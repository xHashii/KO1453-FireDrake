-- [수색대장]라에크

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 14425;

-- [수색대장]라에크 클릭시 퀘스트 체크  

if EVENT == 190 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 4589, NPC, 10, 193);
			 Ret = 1; 
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          NpcMsg(UID, 4590, NPC)
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

local savenum = 442;

-- 시드미셀 등장

if EVENT == 530 then
    SaveEvent(UID, 4313);
    SelectMsg(UID, 1, savenum, 4591, NPC, 4080, 193);
end

-- 438 번의 속성 0번 4번일 경우

local MonsterSub = 0;

if EVENT == 532 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 3, savenum, 4592, NPC, 4228, 535, 4157, 520, 4063, 193);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, savenum, 4593, NPC, 10, 193);
   end
end


if EVENT == 520 then
    Status = CheckEventStatus(UID, 445)
       if Status == 2 then
       SelectMsg(UID, 2, savenum, 4644, NPC, 10, 193);
       else
       SelectMsg(UID, 2, savenum, 4644, NPC, 10, 521);
   end

end


if EVENT == 535 then
    SelectMsg(UID, 4, savenum, 4647, NPC, 22, 533, 23, 534);
end



if EVENT == 521 then
    RunExchange(UID, 505);
    SaveEvent(UID, 4397); --200 획득 
end


if EVENT == 533 then
   SaveEvent(UID, 4314); --수락시
end


if EVENT == 534 then
   SaveEvent(UID, 4317); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 180 then
    SaveEvent(UID, 4316);
    SelectMsg(UID, 1, savenum, 4594, NPC, 14, 193);
end

-- 438의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 536 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- 사냥이 남았을때
      SelectMsg(UID, 2, savenum, 4595, NPC, 10, 193);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 2, savenum, 4596, NPC, 4172, 537, 4173, 193);
   end
end


local Check;

if EVENT == 537 then
  Check = CheckExchange(UID, 490)
   if  Check == true then
   RunExchange(UID, 490);
   SaveEvent(UID, 4315);
   SaveEvent(UID, 4308);
   else
  Ret = 1;	
  end	 
end


--******************************************************--
-- 아르드림의 평화 끝 
--******************************************************--
--Atros Lyot hunt
if EVENT == 9362 then
   SelectMsg(UID, 2, 899, 8685, 14425,  3012, 9363, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9363 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 899, 8685, 14425, 22, 9364, 23, 9365);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 899, 8685, 14425, 10, 168);
   end
end

if EVENT == 9364 then
	SelectMsg(UID, 2, 899, 8685, 14425, 10, 168);
   SaveEvent(UID, 9394);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9365 then
   SaveEvent(UID, 9396);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 899, 8685, 14425, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 899, 8685, 14425, 14, 168);
   end
end

-- 899의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9367 then
   MonsterCount01  = CountMonsterQuestSub(UID, 32001);
   MonsterCount02  = CountMonsterQuestSub(UID, 32002);
    if  MonsterCount01  > 4 and  MonsterCount02  > 4 then
      SelectMsg(UID, 4, 899, 8685, 14425, 10, 9369, 27, 9368);
	else-- 사냥이 완료 되었을때  
	   if MonsterCount01 < 5 then -- A 사냥이 남았을때
      SelectMsg(UID, 2, 899, 8685, 14425, 10, 9368);
	   elseif  MonsterCount02 < 5 then -- B 사냥이 남았을때
      SelectMsg(UID, 2, 899, 8685, 14425, 10, 9378);
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
   SaveEvent(UID, 9395);
	SelectMsg(UID, 2, 320, 8685, 14425, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9365 then
	SelectMsg(UID, 2, 320, 8685, 14425, 10, 168);
end
--Atros Lyot hunt

return Ret;
