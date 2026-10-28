namespace KnightOnline.Database.World;

public readonly record struct MonsterActorKey(ushort Id,Guid Lease);
public sealed record MonsterObserver(MonsterActorKey Key,ushort Zone,float X,float Z,bool Targetable=true);
public enum MonsterAiMode { Idle, Chasing, Attacking, Returning, Dead }
public readonly record struct MonsterAiStatus(MonsterAiMode Mode,MonsterActorKey? Target,int Waypoints);

/// <summary>One server-owned simulation; never calls session code or performs DB I/O.
/// Tick, presence, target selection and attack permits share one short lock.
/// Lock order: simulation -> creature combat. Hit callers release combat first.</summary>
public sealed class MonsterSimulation
{
    private sealed class Brain(WorldZone zone,WorldNpc npc)
    {
        public WorldZone Zone=zone;
        public WorldNpc Npc=npc;
        public string Life=npc.Combat.LifeToken;
        public MonsterAiMode Mode;
        public MonsterActorKey? Target;
        public long EngagedAt,BlockedAt=-1;
        public List<MonsterMotion> Path=[];
    }
    private readonly object _gate=new();
    private readonly Dictionary<ushort,Brain> _brains;
    private readonly Dictionary<ushort,(MonsterObserver Actor,long Seen)> _observers=[];
    private readonly Func<long> _clock;
    private long _lastTick=long.MinValue;
    public MonsterSimulation(WorldCatalog world,Func<long>? clock=null)
    {
        _clock=clock??(()=>Environment.TickCount64);
        _brains=world.Zones.Values.SelectMany(z=>z.Npcs.Where(n=>n.Prototype.IsMonster).Select(n=>new Brain(z,n))).ToDictionary(b=>b.Npc.Id);
        foreach(var b in _brains.Values) b.Npc.Move(b.Life,_clock(),b.Npc.SpawnX,b.Npc.SpawnY,b.Npc.SpawnZ,0);
    }
    public async Task RunAsync(CancellationToken cancellation)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(250));
        try { while(await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false)) Tick(_clock()); }
        catch(OperationCanceledException) when(cancellation.IsCancellationRequested) { }
    }
    public void Publish(MonsterObserver actor)
    {
        lock(_gate)
        {
            if(actor.Key.Id==0||actor.Key.Lease==Guid.Empty||!float.IsFinite(actor.X)||!float.IsFinite(actor.Z)) return;
            if(_observers.TryGetValue(actor.Key.Id,out var old)&&(old.Actor.Key!=actor.Key||old.Actor.Zone!=actor.Zone||old.Actor.Targetable&&!actor.Targetable))
                Invalidate(old.Actor.Key,_clock());
            _observers[actor.Key.Id]=(actor,_clock());
        }
    }
    public void Remove(MonsterActorKey key)
    {
        lock(_gate)
        {
            // Pre-login packets and repeated cleanup must not scan every brain.
            // Publish already invalidates an old lease before replacing its ID.
            if(!_observers.TryGetValue(key.Id,out var old)||old.Actor.Key!=key) return;
            _observers.Remove(key.Id);
            Invalidate(key,_clock());
        }
    }
    private void Invalidate(MonsterActorKey key,long now)
    { foreach(var b in _brains.Values) if(b.Target==key) Return(b,now); }
    private MonsterObserver? Observer(Brain b,MonsterActorKey key,long now,bool targetable)
    {
        if(!_observers.TryGetValue(key.Id,out var p)||p.Actor.Key!=key||p.Actor.Zone!=b.Zone.Id||
           now<p.Seen||now-p.Seen>2000||targetable&&!p.Actor.Targetable||!b.Zone.Map.Contains(p.Actor.X,p.Actor.Z)) return null;
        return p.Actor;
    }
    private static float Distance(float x,float z,float tx,float tz) => MathF.Sqrt((x-tx)*(x-tx)+(z-tz)*(z-tz));
    private static float Leash(Brain b) => Math.Clamp(b.Npc.Prototype.TraceRange,8,80);
    private static bool WithinHome(Brain b,MonsterObserver a) => Distance(b.Npc.SpawnX,b.Npc.SpawnZ,a.X,a.Z)<=Leash(b);
    private static bool Visible(Brain b,MonsterObserver a)
    { var p=b.Npc.Motion;return b.Zone.Map.HasLineOfSight(p.X,p.Z,a.X,a.Z); }
    public bool CanHit(WorldNpc npc,MonsterActorKey key)
    {
        lock(_gate)
        {
            if(!_brains.TryGetValue(npc.Id,out var b)||!ReferenceEquals(npc,b.Npc)||b.Mode==MonsterAiMode.Returning) return false;
            var a=Observer(b,key,_clock(),false);
            return a is not null&&WithinHome(b,a)&&Visible(b,a);
        }
    }
    public void Engage(WorldNpc npc,MonsterActorKey key)
    {
        lock(_gate)
        {
            var now=_clock();
            if(!_brains.TryGetValue(npc.Id,out var b)||!ReferenceEquals(npc,b.Npc)||b.Mode==MonsterAiMode.Returning||npc.Combat.Read(now).Hp<=0) return;
            var life=npc.Combat.Read(now);
            if(b.Life!=life.Token)
            {
                b.Life=life.Token;b.Mode=MonsterAiMode.Idle;b.Target=null;b.Path.Clear();b.BlockedAt=-1;
                npc.Move(b.Life,now,npc.SpawnX,npc.SpawnY,npc.SpawnZ,0);
            }
            var a=Observer(b,key,now,true);
            if(a is null) return; // Hidden GMs may hit but never become AI targets.
            if(!WithinHome(b,a)||!Visible(b,a)) { Return(b,now);return; }
            Acquire(b,key,now);
        }
    }
    private static void Acquire(Brain b,MonsterActorKey key,long now)
    {
        b.EngagedAt=now;
        b.Target=key;b.Mode=MonsterAiMode.Chasing;b.Npc.Combat.ChooseTarget(key.Id,now);
    }
    private static void Return(Brain b,long now)
    {
        if(b.Mode is MonsterAiMode.Dead or MonsterAiMode.Returning) return;
        b.Target=null;b.Mode=MonsterAiMode.Returning;b.BlockedAt=-1;b.Npc.Combat.BeginReturn(now);
    }
    public bool TryAttack(WorldNpc npc,MonsterActorKey key)
    {
        lock(_gate)
        {
            var now=_clock();
            if(!_brains.TryGetValue(npc.Id,out var b)||!ReferenceEquals(npc,b.Npc)||b.Target!=key||b.Mode is not (MonsterAiMode.Chasing or MonsterAiMode.Attacking)) return false;
            var a=Observer(b,key,now,true);var p=npc.Motion;
            if(a is null||!WithinHome(b,a)||!Visible(b,a)||Distance(p.X,p.Z,a.X,a.Z)>npc.Prototype.AttackRange) return false;
            return npc.Combat.Retaliate(key.Id,now,npc.Prototype.AttackDelayMs);
        }
    }
    public MonsterAiStatus Status(ushort npc)
    { lock(_gate) { var b=_brains[npc];return new(b.Mode,b.Target,b.Path.Count); } }
    public void Tick(long now)
    {
        lock(_gate)
        {
            if(_lastTick!=long.MinValue&&now<=_lastTick) return;
            var elapsed=_lastTick==long.MinValue?0:Math.Min((now-_lastTick)/1000f,.5f);_lastTick=now;
            var zones=_observers.Values.Where(p=>now>=p.Seen&&now-p.Seen<=2000&&p.Actor.Targetable)
                .Select(p=>p.Actor).GroupBy(p=>p.Zone).ToDictionary(g=>g.Key,g=>g.ToArray());
            foreach(var b in _brains.Values)
            {
                var npc=b.Npc;var life=npc.Combat.Read(now);
                if(life.Hp<=0) { b.Mode=MonsterAiMode.Dead;b.Target=null;b.Path.Clear();continue; }
                if(b.Life!=life.Token)
                {
                    b.Life=life.Token;b.Mode=MonsterAiMode.Idle;b.Target=null;b.Path.Clear();b.BlockedAt=-1;
                    npc.Move(b.Life,now,npc.SpawnX,npc.SpawnY,npc.SpawnZ,0);
                }
                if(b.Mode==MonsterAiMode.Idle&&npc.Prototype.Behaviour is not (1 or 2 or 6)&&npc.Prototype.SearchRange>0&&zones.TryGetValue(b.Zone.Id,out var nearby))
                {
                    var p=npc.Motion;
                    var target=nearby.Where(a=>b.Zone.Map.Contains(a.X,a.Z)&&WithinHome(b,a)&&Distance(p.X,p.Z,a.X,a.Z)<=Math.Clamp(npc.Prototype.SearchRange,0,40))
                        .OrderBy(a=>Distance(p.X,p.Z,a.X,a.Z)).ThenBy(a=>a.Key.Id).FirstOrDefault(a=>Visible(b,a));
                    if(target is not null) Acquire(b,target.Key,now);
                }
                if(b.Mode is MonsterAiMode.Chasing or MonsterAiMode.Attacking)
                {
                    var target=b.Target is {} key?Observer(b,key,now,true):null;
                    var p=npc.Motion;
                    if(target is null||!WithinHome(b,target)||Distance(npc.SpawnX,npc.SpawnZ,p.X,p.Z)>Leash(b)||now-b.EngagedAt>30000)
                        Return(b,now);
                    else
                    {
                        npc.Combat.ChooseTarget(target.Key.Id,now);
                        var distance=Distance(p.X,p.Z,target.X,target.Z);
                        if(distance<=npc.Prototype.AttackRange&&Visible(b,target))
                        { b.Mode=MonsterAiMode.Attacking;b.EngagedAt=now;b.BlockedAt=-1;npc.Move(b.Life,now,p.X,p.Y,p.Z,0); }
                        else
                        {
                            b.Mode=MonsterAiMode.Chasing;
                            if(!MoveToward(b,target.X,target.Z,npc.Prototype.AttackRange,elapsed,now,true))
                            {
                                npc.Move(b.Life,now,p.X,p.Y,p.Z,0);
                                if(b.BlockedAt<0) b.BlockedAt=now;
                                if(npc.Prototype.MoveSpeed>0&&now-b.BlockedAt>=2000) Return(b,now);
                            }
                            else b.BlockedAt=-1;
                        }
                    }
                }
                if(b.Mode==MonsterAiMode.Returning)
                {
                    var p=npc.Motion;
                    while(b.Path.Count>0&&Distance(p.X,p.Z,b.Path[^1].X,b.Path[^1].Z)<.01f) b.Path.RemoveAt(b.Path.Count-1);
                    var destination=b.Path.Count>0?b.Path[^1]:new MonsterMotion(npc.SpawnX,npc.SpawnY,npc.SpawnZ,0,0,b.Life);
                    if(Distance(p.X,p.Z,npc.SpawnX,npc.SpawnZ)<.01f&&b.Path.Count==0)
                    {
                        npc.Combat.FinishReturn(now);b.Life=npc.Combat.LifeToken;b.Mode=MonsterAiMode.Idle;
                        npc.Move(b.Life,now,npc.SpawnX,npc.SpawnY,npc.SpawnZ,0);
                    }
                    else if(!MoveToward(b,destination.X,destination.Z,0,elapsed,now,false)) npc.Move(b.Life,now,p.X,p.Y,p.Z,0);
                }
            }
        }
    }
    private static bool MoveToward(Brain b,float x,float z,float stop,float elapsed,long now,bool remember)
    {
        var npc=b.Npc;var p=npc.Motion;var distance=Distance(p.X,p.Z,x,z);
        var speed=Math.Clamp(npc.Prototype.MoveSpeed,0,12);
        if(distance<=stop+.0001f||elapsed<=0||speed<=0) return false;
        var step=Math.Min(speed*elapsed,distance-stop);
        var nx=p.X+(x-p.X)*step/distance;var nz=p.Z+(z-p.Z)*step/distance;
        if(Distance(npc.SpawnX,npc.SpawnZ,nx,nz)>Leash(b)||!b.Zone.Map.CanMonsterStep(p.X,p.Z,nx,nz)) return false;
        if(remember&&b.Path.Count>=512) { Return(b,now);return false; }
        if(!npc.Move(b.Life,now,nx,b.Zone.Map.Height(nx,nz),nz,speed)) return false;
        if(remember) b.Path.Add(p);
        return true;
    }
}
