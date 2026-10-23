local UserClass;
local QuestNum;
local Ret = 0;
local NPC =16085;


-- [행상] 헤메스 클릭시 퀘스트 체크  

if EVENT == 165 then
          NpcMsg(UID, 4131, NPC)
end

if EVENT == 168 then -- 그냥 닫기 
    Ret = 1;
end

--******************************************************--
-- 아이디 변경 시작

local savenum = 426
--******************************************************--
if EVENT == 200 then
   ITEM_COUNTA = HowmuchItem(UID, 800032000); -- 아이디 변경 아이템 
    if ITEM_COUNTA > 0 then
       SendNameChange(UID)
    else
    SelectMsg(UID, 2, savenum, 4454, NPC, 10, 168);
    end
end

--******************************************************--
-- 아이디 변경  끝
--******************************************************--

--******************************************************--
-- 클랜 아이디 변경 시작

local savenum = 450
--******************************************************--
if EVENT == 600 then
   ITEM_COUNTA = HowmuchItem(UID, 800086000);
    if ITEM_COUNTA > 0 then
        Check = isClanLeader(UID)
        if Check then 
           SendClanNameChange(UID) --Lua Fonksiyonu Eksik
        else
           SelectMsg(UID, 2, savenum, 4671, NPC, 10, 168);
        end
    else
        SelectMsg(UID, 2, savenum, 4670, NPC, 10, 168);
    end
end

--******************************************************--
-- 클랜 아이디 변경  끝
--******************************************************--


--******************************************************--
-- 재분배 시작(고급)

if EVENT == 300 then -- Skill ve Stat Point Sıfırlama Redisbition Item ile
   ITEM_COUNTA = HowmuchItem(UID, 700001000);
    if ITEM_COUNTA > 0 then
       SelectMsg(UID, 2, savenum, 4456, NPC, 4189, 301, 4190, 302);
    else
       SelectMsg(UID, 2, savenum, 4455, NPC, 10, 168);
    end
end

local Check1 = 0;

if EVENT == 301 then
       RobItem(UID, 700001000, 1)
	   ResetSkillPoints(UID)
       SelectMsg(UID, 2, savenum, 4473, NPC, 10, 168);
end

if EVENT == 302 then
       RobItem(UID, 700001000, 1)
	   ResetStatPoints(UID)
       SelectMsg(UID, 2, savenum, 4474, NPC, 10, 168);
end

local savenum = 451; 

if EVENT == 700 then -- Skill ve Stat Point Sıfırlama Redisbition Item ile (Middle Class)
    Level = CheckLevel(UID)
    if Level > 59 then
       SelectMsg(UID, 2, savenum, 4674, NPC, 10, 168);
    else 
        ITEM_COUNTA = HowmuchItem(UID, 700008000);
        if ITEM_COUNTA > 0 then
           SelectMsg(UID, 2, savenum, 4673, NPC, 4189, 701, 4190, 702);
        else
           SelectMsg(UID, 2, savenum, 4672, NPC, 10, 168);
        end
    end
end

local Skill=0;
local Stat=0;

if EVENT == 701 then
       RobItem(UID, 700008000, 1)
	   ResetSkillPoints(UID)
       SelectMsg(UID, 2, savenum, 4473, NPC, 10, 168);
end

if EVENT == 702 then
       RobItem(UID, 700008000, 1)
	   ResetStatPoints(UID)
       SelectMsg(UID, 2, savenum, 4474, NPC, 10, 168);
end

--******************************************************--
-- 재분배 끝(중급)
--******************************************************--

--******************************************************--
-- 가이드북 이벤트  시작

local savenum = 428
--******************************************************--
if EVENT == 400 then
    SelectMsg(UID, 2, savenum, 4457, NPC, 4192, 401, 4191, 168);
end

if EVENT == 401 then -- 입력 박스 
OpenEditBox(UID, 4459, 402, 403)
end

if EVENT == 402 then --실패시 
    SelectMsg(UID, 2, savenum, 4460, NPC, 10, 168);
end

if EVENT == 403 then --성공시 
Serial=CheckPPCardSerial(UID)
    if Serial == 1 then
    Slot = CheckGiveSlot(UID) 
        if Slot > 0 then
        GivePPCardItem(UID)
        else
        SelectMsg(UID, 2, savenum, 4645, NPC, 10, 168);
        end
    else
    SelectMsg(UID, 2, savenum, 4472, NPC, 10, 168);
    end
end
--******************************************************--
-- 캡틴 프리미엄  시작

if EVENT == 500 then
	PREM = GetPremium(UID);
	if (PREM == 0) then
		SelectMsg(UID, 3, -1, 4462, NPC, 40100, 176, 40101, 181, 40102, 182, 40103, 186);
	else
		SelectMsg(UID, 2, -1, 4462, NPC, 10, 101);
	end
end

if (EVENT == 176) then -- Bronze Premium
	SelectMsg(UID, 2, -1, 4462, NPC, 4006, 183, 4005, 101);
end

if (EVENT == 183) then
	Bronze = HowmuchItem(UID, 800037000);
	if (Bronze < 1) then
		SelectMsg(UID, 2, -1, 4462, NPC, 18, 1000);
	else
		RobItem(UID, 800037000, 1)
		GivePremium(UID, 1, 30)
	end
end

if (EVENT == 181) then -- Silver Premium
	SelectMsg(UID, 2, -1, 4462, NPC, 4006, 184, 4005, 101);
end

if (EVENT == 184) then
	Silver = HowmuchItem(UID, 800038000);
	if (Silver < 1) then
		SelectMsg(UID, 2, -1, 4462, NPC, 18, 1000);
	else
		RobItem(UID, 800038000, 1)
		GivePremium(UID, 2, 30)
	end
end

if (EVENT == 182) then -- Gold Premium
	SelectMsg(UID, 2, -1, 4462, NPC, 4006, 185, 4005, 101);
end

if (EVENT == 185) then
	Gold = HowmuchItem(UID, 800080000);
	if (Gold < 1) then
		SelectMsg(UID, 2, -1, 4462, NPC, 18, 1000);
	else
		RobItem(UID, 800080000, 1)
		GivePremium(UID, 3, 30)
	end
end

if (EVENT == 186) then -- Platinum Premium
	SelectMsg(UID, 2, -1, 4462, NPC, 4006, 187, 4005, 101);
end

if (EVENT == 187) then
	Platinum = HowmuchItem(UID, 800039000);
	if (Platinum < 1) then
		SelectMsg(UID, 2, -1, 4462, NPC, 18, 1000);
	else
		RobItem(UID, 800039000, 1)
		GivePremium(UID, 7, 30)
	end
end
--******************************************************--
-- 캡틴 프리미엄 끝
--******************************************************--
if EVENT == 850 then
   SelectMsg(UID, 3, -1, 4901, NPC, 4285, 558, 4286, 851, 4287, 852);
end

local ITEMARMOR = 0;
local ITEMHELMET = 0;

if EVENT == 558 then
   SelectMsg(UID, 11, savenum, 4432, NPC);
end

if EVENT == 851 then
   ITEMARMOR = HowmuchItem(UID, 800180000);
   if ITEMARMOR > 0 then
      SelectMsg(UID, 3, -1, 4902, NPC, 4288, 853, 4289, 854, 4290, 855, 4291, 856);
   else
      SelectMsg(UID, 2, -1, 4921, NPC, 27, 168);
   end
end

if EVENT == 852 then
   ITEMHELMET = HowmuchItem(UID, 800170000);
   if ITEMHELMET > 0 then
      SelectMsg(UID, 3, -1, 4902, NPC, 4292, 857, 4293, 858, 4294, 859, 4295, 860);
   else
      SelectMsg(UID, 2, -1, 4911, NPC, 27, 168);
   end
end

if EVENT == 853 then
   RobItem(UID, 800180000, 1)
   GiveItem(UID, 508011441, 1, 30)
end

if EVENT == 854 then
   RobItem(UID, 800180000, 1)
   GiveItem(UID, 508011442, 1, 30)
end

if EVENT == 855 then
   RobItem(UID, 800180000, 1)
   GiveItem(UID, 508011443, 1, 30)
end

if EVENT == 856 then
   RobItem(UID, 800180000, 1)
   GiveItem(UID, 508011444, 1, 30)
end

if EVENT == 857 then
   RobItem(UID, 800170000, 1)
   GiveItem(UID, 508013318, 1, 30)
end

if EVENT == 858 then
   RobItem(UID, 800170000, 1)
   GiveItem(UID, 508013319, 1, 30)
end

if EVENT == 859 then
   RobItem(UID, 800170000, 1)
   GiveItem(UID, 508013320, 1, 30)
end

if EVENT == 860 then
   RobItem(UID, 800170000, 1)
   GiveItem(UID, 508013321, 1, 30)
end

