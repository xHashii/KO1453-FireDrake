-- [용병]테일스

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 14430;


if EVENT == 150 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
          -- SelectMsg(UID, 2. -1...........)
			 SelectMsg(UID, 2, -1, 8200, NPC, 10, 153);
			 --SetQuestStep(UID, EVENT, 1); -- 뭘 하는 걸까?
			 Ret = 1; -- 이건 왜 저장 시켜요? 그냥 RETURN은 안되나요?
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          NpcMsg(UID, 8202, NPC)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          EVENT = QuestNum
		end
end

if EVENT == 153 then
    Ret = 1;
end



-- [용병]테일스 클릭시 퀘스트 체크  

-------------------------------
-----------콩가 사냥-----------
-------------------------------

if EVENT == 8270 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8868);
    EVENT = 8271
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8873);
    EVENT = 8271
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8878);
    EVENT = 8271
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8883);
     EVENT = 8271
   end
end

if EVENT == 8271 then
    SelectMsg(UID, 1, 860, 8404, NPC, 3013, 8279);
end

if EVENT == 8279 then
    ShowMap(UID, 561);
end

local MonsterSub = 0;

if EVENT == 8272 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 860, 8405, NPC, 10, 8275);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 860, 8406, NPC, 10, 153);
   end
end

if EVENT == 8275 then
    SelectMsg(UID, 4, 860, 8407, NPC, 22, 8273, 23, 8274);
end

if EVENT == 8273 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8869);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8874);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8879);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8884);
   end
end

if EVENT == 8274 then
   SaveEvent(UID, 8872); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 8280 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8871);
    EVENT = 8281
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8876);
    EVENT = 8281
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8881);
    EVENT = 8281
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8886);
    EVENT = 8281
   end
end

if EVENT == 8281 then
    SelectMsg(UID, 1, 860, 8408, NPC, 3002, 153);
end

if EVENT == 8276 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 20 then -- 사냥이 남았을때
     SelectMsg(UID, 2, 860, 8409, NPC, 10, 8277);
    else -- 사냥이 완료 되었을때 
     SelectMsg(UID, 4, 860, 8410, NPC, 10, 8278, 27, 153);
    end
end

if EVENT == 8277 then
   ShowMap(UID, 511);
end

local Check;

if EVENT == 8278 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 985)
        if  Check == true then        
        RunExchange(UID, 985);
        SaveEvent(UID, 8870);
        SaveEvent(UID, 8849);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 986)
        if  Check == true then        
        RunExchange(UID, 986);
        SaveEvent(UID, 8875);
        SaveEvent(UID, 8849);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 987)
        if  Check == true then        
        RunExchange(UID, 987);
        SaveEvent(UID, 8880);
        SaveEvent(UID, 8849);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 988)
        if  Check == true then        
        RunExchange(UID, 988);
        SaveEvent(UID, 8885);
        SaveEvent(UID, 8849);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end

-------------------------------
----------콩가 사냥 끝---------
-------------------------------

-------------------------------
---------에이프 사냥-----------
-------------------------------

if EVENT == 8600 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8910);
    EVENT = 8601
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8915);
    EVENT = 8601
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8920);
    EVENT = 8601
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8925);
     EVENT = 8601
   end
end

if EVENT == 8601 then
   SelectMsg(UID, 1, 863, 8183, NPC, 3002, 8602);
end

if EVENT == 8602 then
   SelectMsg(UID, 1, 863, 8184, NPC, 4241, 8603);
end

local MonsterSub = 0;

if EVENT == 8603 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 1, 863, 8185, NPC, 10, 8604);
   else-- 몬스터 서브 퀘스트가 있을때
 	SelectMsg(UID, 1, 863, 8186, NPC, 10, 153);
   end
end

if EVENT == 8604 then
    SelectMsg(UID, 1, 863, 8187, NPC, 22, 8605, 23, 8606);
end

if EVENT == 8605 then --수락?
   ShowMap(UID, 541);
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8911);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8916);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8921);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8926);
   end
end

if EVENT == 8606 then
   SelectMsg(UID, 1, 863, 8493, NPC, 10, -1);
   SaveEvent(UID, 8914); -- 거절시 
end

-- 836번 속성 0, 4번일 경우

if EVENT == 8610 then
   SelectMsg(UID, 2, 863, 8202, NPC, 3002, 8611);
end

local MonsterSub = 0;

if EVENT == 8611 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 4, 863, 8189, NPC, 3018, 8612, 3019, 8619);
   else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 863, 8190, NPC, 4242, 153);
   end
end

if EVENT == 8612 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8911);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8916);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8921);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8926);
   end
end

if EVENT == 8619 then
    SaveEvent(UID, 8914); -- 거절시 
end

-- 재료를 다 모았을때 시드와 미셀의 등장 

if EVENT == 8613 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8913);
    EVENT = 8614
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8918);
    EVENT = 8614
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8923);
    EVENT = 8614
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8928);
    EVENT = 8614
   end
end

if EVENT == 8614 then
    SelectMsg(UID, 1, 863, 8415, NPC, 4080, 153);
end

-- 836이 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 8615 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- 사냥이 남았을때
    SelectMsg(UID, 2, 863, 8190, NPC, 10, 8617);
   else-- 사냥이 완료 되었을때 
    SelectMsg(UID, 4, 863, 8413, NPC, 10, 8618, 27, 153);
   end
end

if EVENT == 8617 then
   ShowMap(UID, 541);
end

local Check;

if EVENT == 8618 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 924)
        if  Check == true then        
        RunExchange(UID, 924);
        SaveEvent(UID, 8912);
        SaveEvent(UID, 8891);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 925)
        if  Check == true then        
        RunExchange(UID, 925);
        SaveEvent(UID, 8917);
        SaveEvent(UID, 8891);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 926)
        if  Check == true then        
        RunExchange(UID, 926);
        SaveEvent(UID, 8922);
        SaveEvent(UID, 8891);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 927)
        if  Check == true then        
        RunExchange(UID, 927);
        SaveEvent(UID, 8927);
        SaveEvent(UID, 8891);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end

-------------------------------
--------에이프 사냥 끝---------
-------------------------------

-------------------------------
------버닝 스켈레톤 사냥-------
-------------------------------

if EVENT == 8500 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8952);
    EVENT = 8501
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8957);
    EVENT = 8501
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8962);
    EVENT = 8501
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8967);
    EVENT = 8501
   end
end

if EVENT == 8501 then
    SelectMsg(UID, 1, 817, 8195, NPC, 56, 153);
end

local MonsterSub = 0;

if EVENT == 8502 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 817, 8196, NPC, 10, 8505);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 817, 8406, NPC, 10, 153);
   end
end

if EVENT == 8505 then
    SelectMsg(UID, 4, 817, 8197, NPC, 22, 8503, 23, 8504);
end

if EVENT == 8503 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8953);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8958);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8963);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8968);
   end
end

if EVENT == 8504 then
   SaveEvent(UID, 8956); -- 거절시 
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 8510 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 8955);
    EVENT = 8511
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 8960);
    EVENT = 8511
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 8965);
    EVENT = 8511
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 8970);
    EVENT = 8511
   end
end

if EVENT == 8511 then
    SelectMsg(UID, 1, 817, 8188, NPC, 3007, 153);
end

if EVENT == 8506 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 30 then -- 사냥이 남았을때
       SelectMsg(UID, 2, 817, 8409, NPC, 10, 8507);
    else -- 사냥이 완료 되었을때 
       Class = CheckClass(UID);
        if Class == 1 or Class == 5 or Class == 6 or Class == 3 or Class == 9 or Class == 10 or Class == 4 or Class == 11 or Class == 12 then
        SelectMsg(UID, 4, 817, 8198, NPC, 10, 8508, 27, 153);
        elseif Class == 2 or Class == 7 or Class == 8 then
        SelectMsg(UID, 5, 817, 8172, NPC, 10, 8508, 27, 153);
        end
    end
end

if EVENT == 8507 then
   ShowMap(UID, 509);
end

local Check;

if EVENT == 8508 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 900)
        if  Check == true then        
        RunExchange(UID, 900);
        SaveEvent(UID, 8954);
        SaveEvent(UID, 8933);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 901)
        if  Check == true then        
        RunExchange(UID, 901);
        SaveEvent(UID, 8959);
        SaveEvent(UID, 8933);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 902)
        if  Check == true then        
        RunExchange(UID, 902);
        SaveEvent(UID, 8964);
        SaveEvent(UID, 8933);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 903)
        if  Check == true then        
        RunExchange(UID, 903);
        SaveEvent(UID, 8969);
        SaveEvent(UID, 8933);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end

-------------------------------
-----버닝 스켈레톤 사냥 끝-----
-------------------------------

-------------------------------
-------애쉬나이트 사냥---------
-------------------------------

if EVENT == 8070 then
   SelectMsg(UID, 1, 800, 8148, NPC, 14, 8351);
end

if EVENT == 8071 then
   SaveEvent(UID, 8991);
end

local MonsterSub = 0;

if EVENT == 8072 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
	SelectMsg(UID, 2, 800, 8156, NPC, 10, 8080);
   else-- 몬스터 서브 퀘스트가 있을때
 	SelectMsg(UID, 2, 800, 8406, NPC, 10, 153);
	end
end

if EVENT == 8080 then
   SelectMsg(UID, 4, 800, 8174, NPC, 22, 8073, 23, 8074);
end

if EVENT == 8073 then
   SaveEvent(UID, 8992);
end

if EVENT == 8074 then
   SaveEvent(UID, 8995);
end

-- 사냥 완료시 시드와 미셀의 등장

if EVENT == 8075 then
   SelectMsg(UID, 1, 800, 8215, NPC, 3014, 153);
   SaveEvent(UID, 8994);
end

local MonsterCount = 0;

if EVENT == 8077 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 800, 8409, NPC, 10, 8078);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 800, 8216, NPC, 10, 8079, 27, 153);
   end
end

if EVENT == 8078 then
   ShowMap(UID, 503);
end

if EVENT == 8079 then
   Check = CheckExchange(UID, 800)
   if  Check == true then
   RunExchange(UID, 800);
   SaveEvent(UID, 8993);
   SaveEvent(UID, 8987);
   else
  Ret = 1;	
  end	 
end

-------------------------------
------애쉬나이트 사냥 끝-------
-------------------------------

-------------------------------
----------하웅가 사냥----------
-------------------------------

if EVENT == 8150 then
   SelectMsg(UID, 1, 841, 8221, NPC, 3002, 8151);
end

if EVENT == 8151 then
   SaveEvent(UID, 9003);
end

local MonsterSub = 0;

if EVENT == 8152 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
	SelectMsg(UID, 2, 841, 8222, NPC, 10, 8160);
   else-- 몬스터 서브 퀘스트가 있을때
 	SelectMsg(UID, 2, 841, 8406, NPC, 10, 153);
	end
end

if EVENT == 8160 then
   SelectMsg(UID, 4, 841, 8223, NPC, 22, 8153, 23, 8154);
end

if EVENT == 8153 then
   SaveEvent(UID, 9004);
end

if EVENT == 8154 then
   SaveEvent(UID, 9007);
end

-- 사냥 완료시 시드와 미셀의 등장

if EVENT == 8155 then
   SelectMsg(UID, 1, 841, 8215, NPC, 3014, 153);
   SaveEvent(UID, 9006);
end

local MonsterCount = 0;

if EVENT == 8157 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 210 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 841, 8409, NPC, 10, 8158);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 841, 8224, NPC, 10, 8159, 27, 153);
   end
end

if EVENT == 8158 then
   ShowMap(UID, 515);
end

if EVENT == 8159 then
   Check = CheckExchange(UID, 949)
   if  Check == true then
   RunExchange(UID, 949);
   SaveEvent(UID, 9005);
   SaveEvent(UID, 8999);
   else
  Ret = 1;	
  end	 
end

-------------------------------
---------하웅가 사냥 끝--------
-------------------------------

-------------------------------
--------용아 병사 사냥---------
-------------------------------

if EVENT == 8550 then
   SelectMsg(UID, 1, 866, 8005, NPC, 14, 8551);
end

if EVENT == 8551 then
   SaveEvent(UID, 9051);
end

local MonsterSub = 0;

if EVENT == 8552 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
	SelectMsg(UID, 2, 866, 8006, NPC, 10, 8560);
   else-- 몬스터 서브 퀘스트가 있을때
 	SelectMsg(UID, 2, 866, 8406, NPC, 10, 153);
	end
end

if EVENT == 8560 then
   SelectMsg(UID, 4, 866, 8007, NPC, 22, 8553, 23, 8554);
end

if EVENT == 8553 then
   SaveEvent(UID, 9052);
end

if EVENT == 8554 then
   SaveEvent(UID, 9055);
end

-- 사냥 완료시 시드와 미셀의 등장

if EVENT == 8555 then
   SelectMsg(UID, 1, 866, 8008, NPC, 3014, 153);
   SaveEvent(UID, 9054);
end

local MonsterCount = 0;

if EVENT == 8557 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 866, 8409, NPC, 10, 8558);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 866, 8012, NPC, 10, 8559, 27, 153);
   end
end

if EVENT == 8558 then
   ShowMap(UID, 584);
end

if EVENT == 8559 then
   Check = CheckExchange(UID, 1002)
   if  Check == true then
   RunExchange(UID, 1002);
   SaveEvent(UID, 9053);
   SaveEvent(UID, 9047);
   else
  Ret = 1;	
  end	 
end

-------------------------------
-------용아 병사 사냥 끝-------
-------------------------------

-------------------------------
--------용아 기사 사냥---------
-------------------------------

if EVENT == 9000 then
   SelectMsg(UID, 1, 869, 8005, NPC, 14, 9001);
end

if EVENT == 9001 then
   SaveEvent(UID, 9087);
end

local MonsterSub = 0;

if EVENT == 9002 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
	SelectMsg(UID, 2, 869, 8015, NPC, 10, 9010);
   else-- 몬스터 서브 퀘스트가 있을때
 	SelectMsg(UID, 2, 869, 8406, NPC, 10, 153);
	end
end

if EVENT == 9010 then
   SelectMsg(UID, 4, 869, 8016, NPC, 22, 9003, 23, 9004);
end

if EVENT == 9003 then
   SaveEvent(UID, 9088);
end

if EVENT == 9004 then
   SaveEvent(UID, 9091);
end

-- 사냥 완료시 시드와 미셀의 등장

if EVENT == 9005 then
   SelectMsg(UID, 1, 869, 8008, NPC, 3014, 153);
   SaveEvent(UID, 9090);
end

local MonsterCount = 0;

if EVENT == 9007 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 869, 8409, NPC, 10, 9008);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 869, 8224, NPC, 10, 9009, 27, 153);
   end
end

if EVENT == 9008 then
   ShowMap(UID, 29);
end

if EVENT == 9009 then
   Check = CheckExchange(UID, 943)
   if  Check == true then
   RunExchange(UID, 943);
   SaveEvent(UID, 9089);
   SaveEvent(UID, 9083);
   else
  Ret = 1;	
  end	 
end

-------------------------------
-------용아 기사 사냥 끝-------
-------------------------------
--Sheriff hunt
if EVENT == 9470 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9576);
     EVENT = 9471
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9581);
     EVENT = 9471
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9586);
     EVENT = 9471
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9591);
     EVENT = 9471
   end
end
if EVENT == 9471 then
    SelectMsg(UID, 1, 916, 8765, 14430, 3014, 193);
end

local MonsterSub = 0;

if EVENT == 9472 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 916, 8765, 14430, 10, 9475);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 916, 8765, 14430, 10, 193);
   end
end

if EVENT == 9475 then
    SelectMsg(UID, 4, 916, 8765, 14430, 22, 9473, 23, 9474);
end

if EVENT == 9473 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9577);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9582);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9587);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9592);
   end
end

if EVENT == 9474 then    SaveEvent(UID, 9580);
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 9480 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9579);
   EVENT = 9482
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9584);
   EVENT = 9482
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9589);
   EVENT = 9482
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9594);
   EVENT = 9482
   end
end

if EVENT == 9482 then
   SelectMsg(UID, 1, 916, 8765, 14430, 3002, 193);
end

if EVENT == 9476 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 80 then -- 사냥이 남았을때
     SelectMsg(UID, 2, 916, 8765, 14430, 10, 9477);
    else -- 사냥이 완료 되었을때 
     SelectMsg(UID, 4, 916, 8765, 14430, 10, 9478, 27, 193);
    end
end

if EVENT == 9477 then
   ShowMap(UID, 622);
end

local Check;

if EVENT == 9478 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 1033)
        if  Check == true then        
   RunExchange(UID, 1126);
   SaveEvent(UID, 9578);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 1034)
        if  Check == true then        
   RunExchange(UID, 1127);
   SaveEvent(UID, 9583);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 1035)
        if  Check == true then        
   RunExchange(UID, 1128);
   SaveEvent(UID, 9588);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1036)
        if  Check == true then        
   RunExchange(UID, 1129);
   SaveEvent(UID, 9593);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
--Sheriff hunt
--Garuna hunt
if EVENT == 9490 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9618);
     EVENT = 9491
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9623);
     EVENT = 9491
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9628);
     EVENT = 9491
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9633);
     EVENT = 9491
   end
end
if EVENT == 9491 then
    SelectMsg(UID, 1, 918, 8767, 14430, 3014, 193);
end

local MonsterSub = 0;

if EVENT == 9492 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
    SelectMsg(UID, 2, 918, 8767, 14430, 10, 9495);
	else-- 몬스터 서브 퀘스트가 있을때
    SelectMsg(UID, 2, 918, 8767, 14430, 10, 193);
   end
end

if EVENT == 9495 then
    SelectMsg(UID, 4, 918, 8767, 14430, 22, 9493, 23, 9494);
end

if EVENT == 9493 then --수락시
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9619);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9624);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9629);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9634);
   end
end

if EVENT == 9494 then    SaveEvent(UID, 9622);
end


-- 재료를 다 모았을때 시드 또 등장 

if EVENT == 9500 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9621);
   EVENT = 9502
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9626);
   EVENT = 9502
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9631);
   EVENT = 9502
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9636);
   EVENT = 9502
   end
end

if EVENT == 9502 then
   SelectMsg(UID, 1, 918, 8767, 14430, 3002, 193);
end

if EVENT == 9496 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 50 then -- 사냥이 남았을때
     SelectMsg(UID, 2, 918, 8767, 14430, 10, 9497);
    else -- 사냥이 완료 되었을때 
     SelectMsg(UID, 4, 918, 8767, 14430, 10, 9498, 27, 193);
    end
end

if EVENT == 9497 then
   ShowMap(UID, 626);
end

local Check;

if EVENT == 9498 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 1033)
        if  Check == true then        
   RunExchange(UID, 1134);
   SaveEvent(UID, 9620);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 1034)
        if  Check == true then        
   RunExchange(UID, 1135);
   SaveEvent(UID, 9625);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 1035)
        if  Check == true then        
   RunExchange(UID, 1136);
   SaveEvent(UID, 9630);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1036)
        if  Check == true then        
   RunExchange(UID, 1137);
   SaveEvent(UID, 9635);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
--Garuna hunt

return Ret;