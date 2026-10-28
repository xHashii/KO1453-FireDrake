using KnightOnline.Database.World;
using KnightOnline.Shared.Protocol;

namespace KnightOnline.Verification;

internal static class MonsterAiVerification
{
    private sealed class Harness
    {
        public long Now;
        public WorldCatalog World;
        public MonsterSimulation Ai;
        public WorldNpc Npc;
        public List<MonsterObserver> People=[];
        public Harness(string directory,string map="flat.smd",byte behaviour=4,float speed=4)
        {
            var monster=new NpcPrototype(150,"Kecoon",150,100,0,0,0,0,0,1,100,true,
                AttackDelayMs:1000,AttackRange:2,Behaviour:behaviour,MoveSpeed:speed,SearchRange:12,TraceRange:40);
            var dummy=new NpcPrototype(1,"Merchant",1,100,0,0,0,21,0,1);
            World=new(new([new(1,map)],new Dictionary<ushort,NpcPrototype>{{1,dummy}},
                [new(1,150,20,20,20,20,1,0,true,6)],[new(1,4013,5,212,2,1,1,0,1)],
                new Dictionary<ushort,NpcPrototype>{{150,monster}}),directory);
            Npc=World.Zones[1].Npcs.Single();Ai=new(World,()=>Now);
        }
        public MonsterActorKey Add(ushort id,float x,float z=20,bool targetable=true)
        {
            var key=new MonsterActorKey(id,Guid.NewGuid());People.Add(new(key,1,x,z,targetable));return key;
        }
        public void Start() { foreach(var p in People) Ai.Publish(p);Ai.Tick(Now); }
        public void Step(int milliseconds=250,bool refresh=true)
        { Now+=milliseconds;if(refresh) foreach(var p in People) Ai.Publish(p);Ai.Tick(Now); }
        public void Remove(MonsterActorKey key) { People.RemoveAll(p=>p.Key==key);Ai.Remove(key); }
        public void At(MonsterActorKey key,float x,float z=20)
        { var i=People.FindIndex(p=>p.Key==key);People[i]=People[i] with{X=x,Z=z};Ai.Publish(People[i]); }
    }
    private static void Map(string path,bool wall=false,bool steep=false)
    {
        using var w=new BinaryWriter(File.Create(path));const int size=65;
        w.Write(size);w.Write(1f);
        for(var x=0;x<size;x++) for(var z=0;z<size;z++) w.Write(steep?x*2f:0f);
        w.Write(64f);w.Write(64f);w.Write(wall?2:0);
        if(wall)
            foreach(var v in new[]{24f,0,0,24,5,0,24,5,64,24,0,0,24,5,64,24,0,64}) w.Write(v);
        for(var i=0;i<16;i++) w.Write(0u); // 16-unit main cells
        w.Write(0); // legacy objects
        for(var i=0;i<size*size;i++) w.Write((ushort)0);
        w.Write(0);w.Write(0); // regeneration and warps
    }
    public static void Rules(Action<string,bool> test)
    {
        var directory=Path.Combine(Path.GetTempPath(),"ko-monster-ai-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try
        {
            Map(Path.Combine(directory,"flat.smd"));Map(Path.Combine(directory,"wall.smd"),true);Map(Path.Combine(directory,"steep.smd"),steep:true);
            test("AI move packet literal vector follows native X/Z/Y/speed order",Convert.ToHexString(MonsterMovementPackets.Move(10000,new(10,3,20,4,1,"life")).Payload)=="10276400C8001E002800");
            test("AI move packet signed Y and stop speed",Convert.ToHexString(MonsterMovementPackets.Move(10000,new(10,-3,20,0,1,"life")).Payload)=="10276400C800E2FF0000");
            var h=new Harness(directory);var actor=h.Add(1001,28);h.Start();h.Step();
            test("AI aggressive actor acquires and takes bounded chase step",h.Ai.Status(h.Npc.Id).Target==actor&&h.Npc.X==21&&h.Npc.Motion.Speed==4);
            var motion=h.Npc.Motion;h.Ai.Tick(h.Now);
            test("AI duplicate ticks and additional observers cannot accelerate movement",h.Npc.Motion==motion);
            var other=h.Add(1002,29);h.Step();
            var packetA=MonsterMovementPackets.Move(h.Npc.Id,h.Npc.Motion);var packetB=MonsterMovementPackets.Move(h.Npc.Id,h.Npc.Motion);
            test("AI observers share authoritative position and move bytes",Convert.ToHexString(packetA.Payload)==Convert.ToHexString(packetB.Payload)&&h.Npc.X==22);
            for(var i=0;i<8;i++) h.Step();
            var permits=0;Parallel.For(0,30,_=>{if(h.Ai.TryAttack(h.Npc,actor)) Interlocked.Increment(ref permits);});
            test("AI shared attack cadence permits one attack, not one per viewer",permits==1&&!h.Ai.TryAttack(h.Npc,other));
            h.Step(1000);
            test("AI attack cadence resumes after authored interval",h.Ai.TryAttack(h.Npc,actor));
            var oldLife=h.Npc.Combat.LifeToken;h.Npc.Combat.Hit(10,h.Now,actor.Id);h.Ai.Engage(h.Npc,actor);
            h.At(actor,62);h.Step();
            test("AI hard leash clears target and makes return invulnerable",h.Ai.Status(h.Npc.Id).Mode==MonsterAiMode.Returning&&h.Npc.Combat.Hit(1,h.Now,actor.Id) is null);
            // Remove the second nearby observer to avoid reacquisition after arrival.
            h.Remove(other);
            for(var i=0;i<50;i++) h.Step();
            test("AI reverse-path return reaches home, heals and changes encounter token",h.Npc.X==20&&h.Npc.Z==20&&h.Npc.Combat.Health(h.Now)==100&&h.Npc.Combat.LifeToken!=oldLife&&h.Npc.Combat.Hit(1,h.Now,actor.Id,oldLife) is null);
            var passive=new Harness(directory,behaviour:1);var p=passive.Add(1001,28);passive.Start();passive.Step();
            test("AI passive monsters do not proactively aggro",passive.Ai.Status(passive.Npc.Id).Mode==MonsterAiMode.Idle&&passive.Npc.X==20);
            passive.Npc.Combat.Hit(1,passive.Now,p.Id);passive.Ai.Engage(passive.Npc,p);passive.Step();
            test("AI passive monster chases a valid attacker",passive.Ai.Status(passive.Npc.Id).Target==p&&passive.Npc.X>20);
            passive.Remove(p);var reused=passive.Add(1001,21);passive.Start();
            test("AI ID reuse cannot inherit attack permits",!passive.Ai.TryAttack(passive.Npc,reused)&&!passive.Ai.TryAttack(passive.Npc,p));
            passive.Ai.Remove(p);passive.Step();passive.Step();passive.Npc.Combat.Hit(1,passive.Now,reused.Id);passive.Ai.Engage(passive.Npc,reused);
            test("AI delayed old disconnect does not remove replacement session",passive.Ai.Status(passive.Npc.Id).Target==reused);
            var hidden=new Harness(directory);var gm=hidden.Add(1001,21,targetable:false);hidden.Start();hidden.Step();
            hidden.Npc.Combat.Hit(1,hidden.Now,gm.Id);hidden.Ai.Engage(hidden.Npc,gm);
            test("AI hidden GM neither proactively aggros nor receives retaliation",hidden.Ai.CanHit(hidden.Npc,gm)&&hidden.Ai.Status(hidden.Npc.Id).Target is null&&!hidden.Ai.TryAttack(hidden.Npc,gm));
            var stale=new Harness(directory);stale.Add(1001,28);stale.Start();stale.Step();stale.Step(2100,false);
            test("AI stale presence expires instead of chasing a stalled session",stale.Ai.Status(stale.Npc.Id).Target is null);
            var zone=new Harness(directory);var traveler=zone.Add(1001,28);zone.Start();zone.Step();zone.People[0]=zone.People[0] with{Zone=2};zone.Ai.Publish(zone.People[0]);
            test("AI zone change invalidates target immediately",zone.Ai.Status(zone.Npc.Id).Target is null&&!zone.Ai.TryAttack(zone.Npc,traveler));
            var death=new Harness(directory);var killer=death.Add(1001,28);death.Start();death.Step();var corpse=death.Npc.Motion;var deathLife=death.Npc.Combat.LifeToken;
            death.Npc.Combat.Hit(1000,death.Now,killer.Id);death.Step();death.Remove(killer);death.Step();
            test("AI corpses freeze at death location and cannot attack",death.Npc.X==corpse.X&&death.Npc.Z==corpse.Z&&death.Ai.Status(death.Npc.Id).Mode==MonsterAiMode.Dead&&!death.Ai.TryAttack(death.Npc,killer));
            death.Step(6000);
            test("AI authored respawn returns to spawn and rejects old-life damage",death.Npc.X==20&&death.Npc.Z==20&&death.Npc.Combat.Health(death.Now)==100&&death.Npc.Combat.Hit(1,death.Now,killer.Id,deathLife) is null);
            var hitch=new Harness(directory);hitch.Add(1001,28);hitch.Start();hitch.Step(10000);
            test("AI long scheduler stall never creates catch-up teleport",hitch.Npc.X==22);
            var wall=new Harness(directory,"wall.smd");var across=wall.Add(1001,28);wall.Start();wall.Step();
            test("AI shape collision blocks aggro and damage through walls",wall.Npc.X==20&&!wall.Ai.CanHit(wall.Npc,across)&&wall.Ai.Status(wall.Npc.Id).Target is null);
            var map=wall.World.Zones[1].Map;
            test("AI collision is double-sided and direct movement does not cross wall",!map.HasLineOfSight(20,20,28,20)&&!map.HasLineOfSight(28,20,20,20)&&!map.CanMonsterStep(23,20,25,20));
            wall.At(across,22);wall.Step();wall.At(across,28);
            for(var i=0;i<20;i++) wall.Step();
            test("AI blocked chase returns by safe breadcrumbs, never tunnels",wall.Npc.X<24&&wall.Npc.X==20&&wall.Ai.Status(wall.Npc.Id).Target is null);
            var steep=new Harness(directory,"steep.smd");
            test("AI steep terrain, map edges and excessive steps are rejected",!steep.World.Zones[1].Map.CanMonsterStep(20,20,21,20)&&!map.CanMonsterStep(-1,20,0,20)&&!map.CanMonsterStep(20,20,30,20));
            var stationary=new Harness(directory,speed:0);var archer=stationary.Add(1001,28);stationary.Start();
            for(var i=0;i<16;i++) stationary.Step();
            test("AI zero-speed monsters stay stationary without repeated evade healing",stationary.Npc.X==20&&stationary.Ai.Status(stationary.Npc.Id).Target==archer);
            var concurrent=new Harness(directory);var contender=concurrent.Add(1001,21);concurrent.Start();concurrent.Step(1000);
            Parallel.For(0,80,i=>
            {
                if(i%3==0) { concurrent.Npc.Combat.Hit(1,concurrent.Now,contender.Id);concurrent.Ai.Engage(concurrent.Npc,contender); }
                else if(i%3==1) { concurrent.Ai.Tick(concurrent.Now);concurrent.Ai.TryAttack(concurrent.Npc,contender); }
                else { concurrent.Ai.Remove(contender);concurrent.Ai.Publish(concurrent.People[0]); }
            });
            test("AI concurrent presence, hits and ticks finish with bounded shared state",concurrent.Npc.Combat.Health(concurrent.Now) is >=0 and <=100&&float.IsFinite(concurrent.Npc.Motion.X));
            using var cancel=new CancellationTokenSource();cancel.Cancel();
            stationary.Ai.RunAsync(cancel.Token).GetAwaiter().GetResult();
            test("AI shared scheduler accepts graceful cancellation",true);
        }
        finally { Directory.Delete(directory,true); }
    }
}
