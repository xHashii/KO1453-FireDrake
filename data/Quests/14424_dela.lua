-- [고행자]타베드

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 14424;

-- [고행자]타베드 클릭시 퀘스트 체크  

if EVENT == 190 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 4515, NPC, 10, 193);
			 Ret = 1; 
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          NpcMsg(UID, 4516, NPC)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end

--******************************************************--
-- 일곱 증표의 수호자 시작 
--******************************************************--

local savenum = 430;

-- 430번의 속성 0번 4번일 경우

if EVENT == 532 then   
    SelectMsg(UID, 4, savenum, 4509, NPC, 22, 533, 23, 534);
end

if EVENT == 533 then
   SaveEvent(UID, 4272); --수락시
   GiveItem(UID, 910127000, 1);
end

if EVENT == 534 then
   SaveEvent(UID, 4275); -- 거절시 
end

-- 9번의 속성 1번 3번일 경우

local ItemA
local ItemB

if EVENT == 536 then
ItemA = HowmuchItem(UID, 910134000); --7번째 증표 
ItemB = HowmuchItem(UID, 910127000); -- 명령서
    if  ItemA == 0 then -- 재료가 없을때
       if ItemB == 0 then -- 명령서도  없을때
       GiveItem(UID, 910127000, 1);
       SelectMsg(UID, 2, savenum, 4511, NPC, 10, 193);
       else -- B 재료 없을때 
       SelectMsg(UID, 2, savenum, 4512, NPC, 10, 193);
       end
    else --7번째 열쇠 있을때 
    SelectMsg(UID, 2, savenum, 4510, NPC, 4172, 537, 4173, 193);
   end
end

if EVENT == 537 then
    Check = CheckExchange(UID, 481)
        if  Check == true then
        SaveEvent(UID, 4273);
        RunExchange(UID, 481);
        else
        Ret = 1; 
        end  
end


--******************************************************--
-- 일곱 증표의 수호자 끝 
--******************************************************--

--******************************************************--
-- 몬스터로 부터의 위험 시작 
--******************************************************--

local savenum = 441;

-- 시드미셀 등장

if EVENT == 630 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 4354);
    EVENT = 631
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 4354);
    EVENT = 631
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 4354);
    EVENT = 631
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 4354);
     EVENT = 631
   end
end

if EVENT == 631 then
    SelectMsg(UID, 1, savenum, 4622, NPC, 4080, 193);
end


-- 440 번의 속성 0번 4번일 경우

local MonsterSub = 0;

if EVENT == 632 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 4, savenum, 4623, NPC, 22, 633, 23, 634);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, savenum, 4624, NPC, 10, 193);
   end
end

if EVENT == 633 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 4355);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 4360);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 4365);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 4370);
   end
end


if EVENT == 634 then
   SaveEvent(UID, 4358); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 
    
if EVENT == 280 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 4357);
    EVENT = 281
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 4362);
    EVENT = 281
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 4367);
    EVENT = 281
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 4372);
     EVENT = 281
   end
end

if EVENT == 281 then
    SelectMsg(UID, 1, savenum, 4625, NPC, 14, 193);
end


-- 440의 1번 3번인 경우 

local MonsterCount01
local MonsterCount02
local MonsterCount03
local MonsterCount04


if EVENT == 636 then
   MonsterCount01  = CountMonsterQuestSub(UID, 32001);
   MonsterCount02  = CountMonsterQuestSub(UID, 32002);
   MonsterCount03  = CountMonsterQuestSub(UID, 32003);
   MonsterCount04  = CountMonsterQuestSub(UID, 32004);  
    if  MonsterCount01  > 0 and  MonsterCount02  > 0 and  MonsterCount03  > 0 and  MonsterCount04  > 0 then -- 사냥이 완료 되었을때 
      SelectMsg(UID, 3, savenum, 4630, NPC, 4161, 637, 4162, 193);
    else -- 사냥이 남았을때
       if MonsterCount01 <= 0 then -- A 사냥이 남았을때
       SelectMsg(UID, 2, savenum, 4626, NPC, 10, 193);
       elseif  MonsterCount02 <= 0 then -- B 사냥이 남았을때
       SelectMsg(UID, 2, savenum, 4627, NPC, 10, 193);
       elseif  MonsterCount03 <= 0 then -- C 사냥이 남았을때
       SelectMsg(UID, 2, savenum, 4628, NPC, 10, 193);
       else -- D 사냥이 남았을때
       SelectMsg(UID, 2, savenum, 4629, NPC, 10, 193);
       end
   end
end

local Check;

if EVENT == 637 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then    
    Check = CheckExchange(UID, 497)
        if  Check == true then
        RunExchange(UID, 497);
        SaveEvent(UID, 4356);
        SaveEvent(UID, 4335);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 498)
        if  Check == true then        
        RunExchange(UID, 498);
        SaveEvent(UID, 4361);
        SaveEvent(UID, 4335);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 499)
        if  Check == true then        
        RunExchange(UID, 499);
        SaveEvent(UID, 4366);
        SaveEvent(UID, 4335);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 500)
        if  Check == true then        
        RunExchange(UID, 500);
        SaveEvent(UID, 4371);
        SaveEvent(UID, 4335);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    end
   else
end



--******************************************************--
-- 몬스터로 부터의 위험 끝 
--******************************************************--
--Brahman hunt
if EVENT == 9372 then
   SelectMsg(UID, 2, 900, 8687, 14424,  3012, 9373, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9373 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 900, 8687, 14424, 22, 9374, 23, 9375);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 900, 8687, 14424, 10, 168);
   end
end

if EVENT == 9374 then
	SelectMsg(UID, 2, 900, 8687, 14424, 10, 168);
   SaveEvent(UID, 9406);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9376 then
   SaveEvent(UID, 9408);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 900, 8687, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 900, 8687, 14424, 14, 168);
   end
end

-- 900의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9377 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 900, 8687, 14424, 10, 9378);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 900, 8687, 14424, 10, 9379, 27, 9378);
   end
end

if EVENT == 9378 then
   ShowMap(UID, 606);
end

local Check;

if EVENT == 9379 then
   Check = CheckExchange(UID, 1095)
   if  Check == true then
   RunExchange(UID, 1095);
   SaveEvent(UID, 9407);
	SelectMsg(UID, 2, 320, 8687, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9375 then
	SelectMsg(UID, 2, 320, 8687, 14424, 10, 168);
end
--Brahman hunt
--Crimson Wing hunt
if EVENT == 9382 then
   SelectMsg(UID, 2, 902, 8689, 14424,  3012, 9383, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9383 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 902, 8689, 14424, 22, 9384, 23, 9385);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 902, 8689, 14424, 10, 168);
   end
end

if EVENT == 9384 then
	SelectMsg(UID, 2, 902, 8689, 14424, 10, 168);
   SaveEvent(UID, 9418);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9386 then
   SaveEvent(UID, 9420);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 902, 8689, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 902, 8689, 14424, 14, 168);
   end
end

-- 902의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9387 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 902, 8689, 14424, 10, 9388);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 902, 8689, 14424, 10, 9389, 27, 9388);
   end
end

if EVENT == 9388 then
   ShowMap(UID, 608);
end

local Check;

if EVENT == 9389 then
   Check = CheckExchange(UID, 1097)
   if  Check == true then
   RunExchange(UID, 1097);
   SaveEvent(UID, 9419);
	SelectMsg(UID, 2, 320, 8689, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9385 then
	SelectMsg(UID, 2, 320, 8689, 14424, 10, 168);
end
--Crimson Wing hunt
--Gargoyle hunt
if EVENT == 9392 then
   SelectMsg(UID, 2, 904, 8691, 14424,  3012, 9393, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9393 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 904, 8691, 14424, 22, 9394, 23, 9395);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 904, 8691, 14424, 10, 168);
   end
end

if EVENT == 9394 then
	SelectMsg(UID, 2, 904, 8691, 14424, 10, 168);
   SaveEvent(UID, 9430);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9396 then
   SaveEvent(UID, 9432);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 904, 8691, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 904, 8691, 14424, 14, 168);
   end
end

-- 904의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9397 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 904, 8691, 14424, 10, 9398);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 904, 8691, 14424, 10, 9399, 27, 9398);
   end
end

if EVENT == 9398 then
   ShowMap(UID, 610);
end

local Check;

if EVENT == 9399 then
   Check = CheckExchange(UID, 1099)
   if  Check == true then
   RunExchange(UID, 1099);
   SaveEvent(UID, 9431);
	SelectMsg(UID, 2, 320, 8691, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9395 then
	SelectMsg(UID, 2, 320, 8691, 14424, 10, 168);
end
--Gargoyle hunt
--Apostle of Piercing Cold  hunt
if EVENT == 9412 then
   SelectMsg(UID, 2, 907, 8695, 14424,  3012, 9413, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9413 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 907, 8695, 14424, 22, 9414, 23, 9415);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 907, 8695, 14424, 10, 168);
   end
end

if EVENT == 9414 then
	SelectMsg(UID, 2, 907, 8695, 14424, 10, 168);
   SaveEvent(UID, 9454);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9416 then
   SaveEvent(UID, 9456);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 907, 8695, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 907, 8695, 14424, 14, 168);
   end
end

-- 907의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9417 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 907, 8695, 14424, 10, 9418);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 907, 8695, 14424, 10, 9419, 27, 9418);
   end
end

if EVENT == 9418 then
   ShowMap(UID, 615);
end

local Check;

if EVENT == 9419 then
   Check = CheckExchange(UID, 1102)
   if  Check == true then
   RunExchange(UID, 1102);
   SaveEvent(UID, 9455);
	SelectMsg(UID, 2, 320, 8695, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9415 then
	SelectMsg(UID, 2, 320, 8695, 14424, 10, 168);
end
--Apostle of Piercing Cold  hunt
--Apostle of Flames  hunt
if EVENT == 9422 then
   SelectMsg(UID, 2, 910, 8697, 14424,  3012, 9423, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9423 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 910, 8697, 14424, 22, 9424, 23, 9425);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 910, 8697, 14424, 10, 168);
   end
end

if EVENT == 9424 then
	SelectMsg(UID, 2, 910, 8697, 14424, 10, 168);
   SaveEvent(UID, 9466);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9426 then
   SaveEvent(UID, 9468);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 910, 8697, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 910, 8697, 14424, 14, 168);
   end
end

-- 910의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9427 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 910, 8697, 14424, 10, 9428);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 910, 8697, 14424, 10, 9429, 27, 9428);
   end
end

if EVENT == 9428 then
   ShowMap(UID, 617);
end

local Check;

if EVENT == 9429 then
   Check = CheckExchange(UID, 1105)
   if  Check == true then
   RunExchange(UID, 1105);
   SaveEvent(UID, 9467);
	SelectMsg(UID, 2, 320, 8697, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9425 then
	SelectMsg(UID, 2, 320, 8697, 14424, 10, 168);
end
--Apostle of Flames  hunt
--Doom Soldier hunt
if EVENT == 9542 then
   SelectMsg(UID, 2, 924, 8773, 14424,  3012, 9543, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9543 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 924, 8773, 14424, 22, 9544, 23, 9545);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 924, 8773, 14424, 10, 168);
   end
end

if EVENT == 9544 then
	SelectMsg(UID, 2, 924, 8773, 14424, 10, 168);
   SaveEvent(UID, 9730);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9546 then
   SaveEvent(UID, 9732);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 924, 8773, 14424, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 924, 8773, 14424, 14, 168);
   end
end

-- 924의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9547 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 924, 8773, 14424, 10, 9548);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 924, 8773, 14424, 10, 9549, 27, 9548);
   end
end

if EVENT == 9548 then
   ShowMap(UID, 628);
end

local Check;

if EVENT == 9549 then
   Check = CheckExchange(UID, 1155)
   if  Check == true then
   RunExchange(UID, 1155);
   SaveEvent(UID, 9731);
	SelectMsg(UID, 2, 320, 8773, 14424, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9545 then
	SelectMsg(UID, 2, 320, 8773, 14424, 10, 168);
end
--Doom Soldier hunt
return Ret;
