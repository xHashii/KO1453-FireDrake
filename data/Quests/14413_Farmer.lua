-- [농장관리인]

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;

-- [농장관리인] 농장관리인클릭시 퀘스트 체크  

if EVENT == 190 then
	QuestNum = SearchQuest(UID, 14413);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
          -- SelectMsg(UID, 2. -1...........)
			 SelectMsg(UID, 2, -1, 324, 14413, 10, 193);
			 --SetQuestStep(UID, EVENT, 1); -- 뭘 하는 걸까?
			 Ret = 1; -- 이건 왜 저장 시켜요? 그냥 RETURN은 안되나요?
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          NpcMsg(UID, 325, 14413)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end

--타이온 사냥?

if EVENT == 195 then
   SelectMsg(UID, 1, 108, 327, 14413, 28, 196);
end

if EVENT == 196 then
   ShowMap(UID, 13);
   SaveEvent(UID, 114);
end

--  102번의 속성 0번 4번일 경우

if EVENT == 200 then
   SelectMsg(UID, 2, 108, 328, 14413, 10, 201);
end

if EVENT == 201 then
   SelectMsg(UID, 4, 108, 329, 14413, 22, 202, 23, 193);
end

if EVENT == 202 then
   SaveEvent(UID, 115);
end

-- 재료를 다 모았을 때 
local NATION = 0;--국가 체크

if EVENT == 205 then
   SaveEvent(UID, 117);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 108, 330, 14413, 32, 193);
   else -- 엘모일때
      SelectMsg(UID, 1, 108, 331, 14413, 21, 193);
   end
end


local ItemA ;
local ITEM_COUNT;
local RUN_EXCHANGE ;--재료 교체 함수 만들어 주세요 

if EVENT == 210 then
      ITEM_COUNT = GetMaxExchange(UID, 31); --exchange 테이블 인덱스값       
   if  ITEM_COUNT == 0 then -- 재료 없을때
      SelectMsg(UID, 2, 109, 332, 14413, 18, 212);
   else  -- 재료 있을 때
      SelectMsg(UID, 2, 109, 333, 14413, 10, 213);
   end
end

if EVENT == 212 then
   ShowMap(UID, 14);
end

local Check;


if EVENT == 213 then
  Check = CheckExchange(UID, 31)
   if  Check == true then
   ITEM_COUNT = GetMaxExchange(UID, 31);
 	RunCountExchange(UID, 31, ITEM_COUNT);	   
      SaveEvent(UID, 118);
   else
  Ret = 1;	
  end	 
end
--Tyon hunt
if EVENT == 9530 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9702);
     EVENT = 9531
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9707);
     EVENT = 9531
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9712);
     EVENT = 9531
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9717);
     EVENT = 9531
   end
end
if EVENT == 9531 then
    SelectMsg(UID, 1, 922, 8771, 14413, 3014, 193);
end

local MonsterSub = 0;

if EVENT == 9532 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 922, 8771, 14413, 10, 9535);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 922, 8771, 14413, 10, 193);
   end
end

if EVENT == 9535 then
    SelectMsg(UID, 4, 922, 8771, 14413, 22, 9533, 23, 9534);
end

if EVENT == 9533 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9703);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9708);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9713);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9718);
   end
end

if EVENT == 9534 then    SaveEvent(UID, 9706);
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 9540 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9705);
   EVENT = 9542
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9710);
   EVENT = 9542
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9715);
   EVENT = 9542
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9720);
   EVENT = 9542
   end
end

if EVENT == 9542 then
   SelectMsg(UID, 1, 922, 8771, 14413, 3002, 193);
end

if EVENT == 9536 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 80 then -- 사냥이 남았을때
     SelectMsg(UID, 2, 922, 8771, 14413, 10, 9537);
    else -- 사냥이 완료 되었을때 
     SelectMsg(UID, 4, 922, 8771, 14413, 10, 9538, 27, 193);
    end
end

if EVENT == 9537 then
   ShowMap(UID, 14);
end

local Check;

if EVENT == 9538 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 1033)
        if  Check == true then        
   RunExchange(UID, 1150);
   SaveEvent(UID, 9704);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 1034)
        if  Check == true then        
   RunExchange(UID, 1151);
   SaveEvent(UID, 9709);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 1035)
        if  Check == true then        
   RunExchange(UID, 1152);
   SaveEvent(UID, 9714);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1036)
        if  Check == true then        
   RunExchange(UID, 1153);
   SaveEvent(UID, 9719);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
--Tyon hunt


return Ret;