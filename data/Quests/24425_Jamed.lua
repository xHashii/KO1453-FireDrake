-- [수색대장]자메드

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 24425;

-- [수색대장]자메드 클릭시 퀘스트 체크  

if EVENT == 190 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 4578, NPC, 10, 193);
			 Ret = 1; 
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          NpcMsg(UID, 4579, NPC)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end

--******************************************************--
-- 아르드림의 평화 시작 
--******************************************************--

local savenum = 438;

-- 시드미셀 등장

if EVENT == 530 then
    SaveEvent(UID, 4306);
    SelectMsg(UID, 1, savenum, 4580, NPC, 4080, 193);
end

-- 438 번의 속성 0번 4번일 경우

local MonsterSub = 0;

if EVENT == 532 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 3, savenum, 4582, NPC, 4228, 535, 4157, 520, 4063, 193);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, savenum, 4583, NPC, 10, 193);
   end
end

if EVENT == 535 then
    SelectMsg(UID, 4, savenum, 4646, NPC, 22, 533, 23, 534);
end


if EVENT == 520 then
    Status = CheckEventStatus(UID, 445)
       if Status == 2 then
       SelectMsg(UID, 2, savenum, 4643, NPC, 10, 193);
       else
       SelectMsg(UID, 2, savenum, 4643, NPC, 10, 521);
   end

end


if EVENT == 521 then
    RunExchange(UID, 505);
    SaveEvent(UID, 4398); --200 획득 
end


if EVENT == 533 then
   SaveEvent(UID, 4307); --수락시
end


if EVENT == 534 then
   SaveEvent(UID, 4310); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 180 then
    SaveEvent(UID, 4309);
    SelectMsg(UID, 1, savenum, 4585, NPC, 14, 193);
end

-- 438의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 536 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- 사냥이 남았을때
      SelectMsg(UID, 2, savenum, 4587, NPC, 10, 193);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 2, savenum, 4588, NPC, 4172, 537, 4173, 193);
   end
end


local Check;

if EVENT == 537 then
  Check = CheckExchange(UID, 489)
   if  Check == true then
   RunExchange(UID, 489);
   SaveEvent(UID, 4308);
   SaveEvent(UID, 4315);
   else
  Ret = 1;	
  end	 
end


--******************************************************--
-- 아르드림의 평화 끝 
--******************************************************--
--Atros Lyot hunt
if EVENT == 9362 then
   SelectMsg(UID, 2, 899, 8684, 24425,  3012, 9363, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9363 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 899, 8684, 24425, 22, 9364, 23, 9365);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 899, 8684, 24425, 10, 168);
   end
end

if EVENT == 9364 then
	SelectMsg(UID, 2, 899, 8684, 24425, 10, 168);
   SaveEvent(UID, 9388);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9365 then
   SaveEvent(UID, 9390);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 899, 8684, 24425, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 899, 8684, 24425, 14, 168);
   end
end

-- 899의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9367 then
   MonsterCount01  = CountMonsterQuestSub(UID, 32001);
   MonsterCount02  = CountMonsterQuestSub(UID, 32002);
    if  MonsterCount01  > 4 and  MonsterCount02  > 4 then
      SelectMsg(UID, 4, 899, 8684, 24425, 10, 9369, 27, 9368);
	else-- 사냥이 완료 되었을때  
	   if MonsterCount01 < 5 then -- A 사냥이 남았을때
      SelectMsg(UID, 2, 899, 8684, 24425, 10, 9368);
	   elseif  MonsterCount02 < 5 then -- B 사냥이 남았을때
      SelectMsg(UID, 2, 899, 8684, 24425, 10, 9378);
	   end
   end
end

if EVENT == 9368 then
   ShowMap(UID, 602);
end

if EVENT == 9378 then
   ShowMap(UID, 603);
end

local Check;

if EVENT == 9369 then
   Check = CheckExchange(UID, 1094)
   if  Check == true then
   RunExchange(UID, 1094);
   SaveEvent(UID, 9389);
	SelectMsg(UID, 2, 320, 8684, 24425, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9365 then
	SelectMsg(UID, 2, 320, 8684, 24425, 10, 168);
end
--Atros Lyot hunt

return Ret;
