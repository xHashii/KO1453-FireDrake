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
	QuestNum = SearchQuest(UID, 24413);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
          -- SelectMsg(UID, 2. -1...........)
			 SelectMsg(UID, 2, -1, 1245, 24413, 10, 193);
			 --SetQuestStep(UID, EVENT, 1); -- 뭘 하는 걸까?
			 Ret = 1; -- 이건 왜 저장 시켜요? 그냥 RETURN은 안되나요?
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          NpcMsg(UID, 1246, 24413)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end

--타이온 사냥?

if EVENT == 195 then
   SelectMsg(UID, 1, 109, 326, 24413, 28, 196);
end

if EVENT == 196 then
   ShowMap(UID, 15);
   SaveEvent(UID, 122);
end

--  102번의 속성 0번 4번일 경우

if EVENT == 200 then
   SelectMsg(UID, 2, 109, 328, 24413, 10, 201);
end

if EVENT == 201 then
   SelectMsg(UID, 4, 109, 1247, 24413, 22, 202, 23, 193);
end

if EVENT == 202 then
   SaveEvent(UID, 122);
end

-- 재료를 다 모았을 때 
local NATION = 0;--국가 체크

if EVENT == 205 then
   SaveEvent(UID,124);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 109, 330, 24413, 32, 193);
   else -- 엘모일때
      SelectMsg(UID, 1, 109, 331, 24413, 21, 193);
   end
end


local ItemA ;
local ITEM_COUNT;
local RUN_EXCHANGE ;--재료 교체 함수 만들어 주세요 

if EVENT == 210 then
   ITEM_COUNT = GetMaxExchange(UID, 32); --exchange 테이블 인덱스값    
if  ITEM_COUNT == 0 then -- 재료 없을때
      SelectMsg(UID, 2, 109, 1248, 24413, 18, 193);
   else  -- 재료 있을 때
      SelectMsg(UID, 2, 109, 1249, 24413, 10, 213);
   end
end


if EVENT == 212 then
   ShowMap(UID, 14);
end

local Check;

if EVENT == 213 then
  Check = CheckExchange(UID, 32)
   if  Check == true then
   ITEM_COUNT = GetMaxExchange(UID, 32);
 	RunCountExchange(UID, 31, ITEM_COUNT);	   
   SaveEvent(UID, 125);
   else
  Ret = 1;	
  end	 
end
--Tyon hunt
if EVENT == 9530 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9681);
     EVENT = 9531
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9686);
     EVENT = 9531
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9691);
     EVENT = 9531
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9696);
     EVENT = 9531
   end
end
if EVENT == 9531 then
    SelectMsg(UID, 1, 921, 8770, 24413, 3014, 193);
end

local MonsterSub = 0;

if EVENT == 9532 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 921, 8770, 24413, 10, 9535);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 921, 8770, 24413, 10, 193);
   end
end

if EVENT == 9535 then
    SelectMsg(UID, 4, 921, 8770, 24413, 22, 9533, 23, 9534);
end

if EVENT == 9533 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9682);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9687);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9692);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9697);
   end
end

if EVENT == 9534 then    SaveEvent(UID, 9685);
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 9540 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9684);
   EVENT = 9542
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9689);
   EVENT = 9542
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9694);
   EVENT = 9542
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9699);
   EVENT = 9542
   end
end

if EVENT == 9542 then
   SelectMsg(UID, 1, 921, 8770, 24413, 3002, 193);
end

if EVENT == 9536 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 80 then -- 사냥이 남았을때
     SelectMsg(UID, 2, 921, 8770, 24413, 10, 9537);
    else -- 사냥이 완료 되었을때 
     SelectMsg(UID, 4, 921, 8770, 24413, 10, 9538, 27, 193);
    end
end

if EVENT == 9537 then
   ShowMap(UID, 344);
end

local Check;

if EVENT == 9538 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 1033)
        if  Check == true then        
   RunExchange(UID, 1146);
   SaveEvent(UID, 9683);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 1034)
        if  Check == true then        
   RunExchange(UID, 1147);
   SaveEvent(UID, 9688);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 1035)
        if  Check == true then        
   RunExchange(UID, 1148);
   SaveEvent(UID, 9693);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1036)
        if  Check == true then        
   RunExchange(UID, 1149);
   SaveEvent(UID, 9698);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
--Tyon hunt


return Ret;