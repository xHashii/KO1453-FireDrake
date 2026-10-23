-- [전진기지대장]델라

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 14426;

-- [전진기지대장]델라 클릭시 퀘스트 체크  

if EVENT == 190 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 4605, NPC, 10, 193);
			 Ret = 1; 
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          NpcMsg(UID, 4606, NPC)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end
--******************************************************--
-- 로나크 랜드의 평화 시작 
--******************************************************--

local savenum = 443;

-- 시드미셀 등장

if EVENT == 530 then
    SaveEvent(UID, 4327);
    SelectMsg(UID, 1, savenum, 4607, NPC, 4080, 193, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
end

-- 438 번의 속성 0번 4번일 경우

local MonsterSub = 0;

if EVENT == 532 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 4, savenum, 4608, NPC, 22, 533, 23, 534, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, savenum, 4609, NPC, 10, 193, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
   end
end

if EVENT == 533 then
   SaveEvent(UID, 4328); --수락시
end


if EVENT == 534 then
   SaveEvent(UID, 4331); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 180 then
    SaveEvent(UID, 4330);
    SelectMsg(UID, 1, savenum, 4610, NPC, 14, 193, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
end

-- 438의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 536 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 100 then -- 사냥이 남았을때
      SelectMsg(UID, 2, savenum, 4611, NPC, 10, 193, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 2, savenum, 4612, NPC, 4172, 537, 4173, 193, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1);
   end
end


local Check;

if EVENT == 537 then
  Check = CheckExchange(UID, 492)
   if  Check == true then
   RunExchange(UID, 492);
   SaveEvent(UID, 4329);
   SaveEvent(UID, 4322);
   else
  Ret = 1;	
  end	 
end


--******************************************************--
-- 로나크 랜드의 평화 끝 
--******************************************************--
--Duke Bishop Bach hunt
if EVENT == 9402 then
   SelectMsg(UID, 2, 906, 8693, 14426,  3012, 9403, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9403 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 906, 8693, 14426, 22, 9404, 23, 9405);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 906, 8693, 14426, 10, 168);
   end
end

if EVENT == 9404 then
	SelectMsg(UID, 2, 906, 8693, 14426, 10, 168);
   SaveEvent(UID, 9442);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9405 then
   SaveEvent(UID, 9444);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 906, 8693, 14426, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 906, 8693, 14426, 14, 168);
   end
end

-- 906의 1번 3번인 경우 

local MonsterCount01 = 0;
local MonsterCount02 = 0;
local MonsterCount03 = 0;

if EVENT == 9407 then
   MonsterCount01  = CountMonsterQuestSub(UID, 32001);
   MonsterCount02  = CountMonsterQuestSub(UID, 32002);
   MonsterCount03  = CountMonsterQuestSub(UID, 32003);
    if  MonsterCount01  > 0 and  MonsterCount02  > 0 and  MonsterCount03  > 0  then -- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 906, 8693, 14426, 10, 9409, 27, 9408);
   else-- 사냥이 완료 되었을때    
	   if MonsterCount01 < 1 then -- A 사냥이 남았을때
      SelectMsg(UID, 2, 906, 8693, 14426, 10, 9419);
	   elseif  MonsterCount02 < 1 then -- B 사냥이 남았을때
      SelectMsg(UID, 2, 906, 8693, 14426, 10, 9408);
	   elseif  MonsterCount03 < 1 then -- C 사냥이 남았을때
      SelectMsg(UID, 2, 906, 8693, 14426, 10, 9418);
	   end
   end
end

if EVENT == 9408 then
   ShowMap(UID, 612);
end

if EVENT == 9418 then
   ShowMap(UID, 613);
end

if EVENT == 9419 then
   ShowMap(UID, 614);
end

local Check;

if EVENT == 9409 then
   Check = CheckExchange(UID, 1101)
   if  Check == true then
   RunExchange(UID, 1101);
   SaveEvent(UID, 9443);
	SelectMsg(UID, 2, 320, 8693, 14426, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9405 then
	SelectMsg(UID, 2, 320, 8693, 14426, 10, 168);
end
--Duke Bishop Bach hunt

return Ret;
