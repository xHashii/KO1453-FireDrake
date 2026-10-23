-- [¡Æ©¡¨¬n¨¬¢¥]¨¬¡Ì¨ö¨¬

-- EVENT ¢¥A 100©ö©ª AI¡ío ¨¬IAI ¡íc¢¯eCI¢Òo

-- UID : ¨ù¡©©öo¢¯¢®¨ù¡© A|¡Æ©ªCI¢¥A A?Au©ö©ªE¡Ì
-- EVENT : ¨ù¡©©öo¢¯¢®¨ù¡© A|¡Æ©ªCI¢¥A Au¨ö¨¬¨¡¢ç ©ö©ªE¡Ì
-- STEP : ¨ù¡©©öo¢¯¢®¨ù¡© A|¡Æ©ªCI¢¥A Au¨ö¨¬¨¡¢ç ©ø¡í¨¬I ¢¥U¡Æe

-- A¡×AC ¨ù¨ù¡Æ¢®Ao ¨¡A¢Òo¢¬?A¢¬¢¥A ¡¤c¨ú¨¡ ¨öCCa¨öA C¡¿¡ío Au¢¯¨£¨¬?¨ùo¡¤I A|¡Æ©ª?

-- Ao¢¯¨£¨¬?¨ùo ¨ù¡¾¨ú?...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 14431;


if EVENT == 155 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --C¨ª¢¥c NPC¢¯¢®¡ÆO CO¨ùo AO¢¥A Au¨ö¨¬¨¡¢ç¡Æ¢® 0¡Æ©ø AI¢Ò¡× 
          -- SelectMsg(UID, 2. -1...........)
			 SelectMsg(UID, 2, -1, 8256, NPC, 10, 158);
			 --SetQuestStep(UID, EVENT, 1); -- ©ö¡í CI¢¥A ¡ÆE¡¾i?
			 Ret = 1; -- AI¡ÆC ¢¯O AuAa ¨öAAN¢¯a? ¡¾¡¿©øE RETURNA¨¬ ¨úE¥ìC©ø¨£¢¯a?
		elseif QuestNum > 1 and  QuestNum < 100 then--C¨ª¢¥c NPC¢¯¢®¡ÆO CO¨ùo AO¢¥A Au¨ö¨¬¨¡¢ç¡Æ¢® 1¡Æ©ø AI¢Ò¡× 
          NpcMsg(UID, 8258, NPC)
      else --C¨ª¢¥c NPC¢¯¢®¡ÆO CO¨ùo AO¢¥A Au¨ö¨¬¨¡¢ç¡Æ¢® 1¡Æ©ø AI¡ío AI¢Ò¡× 
          EVENT = QuestNum
		end
end

if EVENT == 158 then
    Ret = 1;
end



-- [¡Æ©¡¨¬n¨¬¢¥]¨¬¡Ì¨ö¨¬ A¡ş¢¬?¨öA Au¨ö¨¬¨¡¢ç A¨ùA¨Ï  

-------------------------------
---------¢Òo©öI¨ú¨¡ ¡íc©øE-----------
-------------------------------

if EVENT == 8250 then
   SelectMsg(UID, 1, 843, 8242, NPC, 28, 8251);
end

if EVENT == 8251 then
   ShowMap(UID, 563);
   SaveEvent(UID, 9015);
end

local MonsterSub = 0;

if EVENT == 8252 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
	SelectMsg(UID, 2, 843, 8243, NPC, 10, 8260);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 2, 843, 8265, NPC, 10, 158);
	end
end

if EVENT == 8260 then
   SelectMsg(UID, 4, 843, 8244, NPC, 22, 8253, 23, 8254);
end

if EVENT == 8253 then
   SaveEvent(UID, 9016);
end

if EVENT == 8254 then
   SaveEvent(UID, 9019);
end

-- ¡íc©øE ¢¯I¡¤a¨öA ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa
if EVENT == 8255 then
   SelectMsg(UID, 1, 843, 8266, NPC, 3007, 158);
   SaveEvent(UID, 9018);
end

local MonsterCount = 0;

if EVENT == 8257 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
      SelectMsg(UID, 2, 843, 8267, NPC, 10, 8258);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
      SelectMsg(UID, 4, 843, 8268, NPC, 10, 8259, 27, 158);
   end
end

if EVENT == 8258 then
   ShowMap(UID, 513);
end

if EVENT == 8259 then
   Check = CheckExchange(UID, 1004)
   if  Check == true then
   RunExchange(UID, 1004);
   SaveEvent(UID, 9017);
   SaveEvent(UID, 9011);
   else
  Ret = 1;	
  end	 
end

-------------------------------
--------¢Òo©öI¨ú¨¡ ¡íc©øE ©ø¢®---------
-------------------------------

-------------------------------
-------¢¯i¢¬¡ÌA¨Ï CIAI ¡íc©øE--------
-------------------------------

if EVENT == 8350 then
   SelectMsg(UID, 1, 845, 8274, NPC, 14, 8351);
end

if EVENT == 8351 then
    SaveEvent(UID, 9027);
end

local MonsterSub = 0;

if EVENT == 8352 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
	SelectMsg(UID, 2, 845, 8275, NPC, 10, 8360);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 2, 845, 8265, NPC, 10, 158);
	end
end

if EVENT == 8360 then
   SelectMsg(UID, 4, 845, 8276, NPC, 22, 8353, 23, 8354);
end

if EVENT == 8353 then
   SaveEvent(UID, 9028);
end

if EVENT == 8354 then
   SaveEvent(UID, 9031);
end

-- ¡íc©øE ¢¯I¡¤a¨öA ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa
if EVENT == 8355 then
   SelectMsg(UID, 1, 845, 8266, NPC, 3007, 158);
   SaveEvent(UID, 9030);
end

local MonsterCount = 0;

if EVENT == 8357 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
      SelectMsg(UID, 2, 845, 8267, NPC, 10, 8358);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
      SelectMsg(UID, 4, 845, 8268, NPC, 10, 8359, 27, 158);
   end
end

if EVENT == 8358 then
   ShowMap(UID, 543);
end

if EVENT == 8359 then
   Check = CheckExchange(UID, 1006)
   if  Check == true then
   RunExchange(UID, 1006);
   SaveEvent(UID, 9029);
   SaveEvent(UID, 9023);
   else
  Ret = 1;	
  end	 
end

-------------------------------
-----¢¯i¢¬¡ÌA¨Ï CIAI ¡íc©øE ©ø¢®-------
-------------------------------

-------------------------------
---------¨¡¢ç¡¤¡í¨¡¢ç ¡íc©øE-----------
-------------------------------

if EVENT == 8650 then
   SelectMsg(UID, 1, 850, 8291, NPC, 3008, 8651);
end

if EVENT == 8651 then
   SelectMsg(UID, 1, 850, 8292, NPC, 4080, 8652);
   SaveEvent(UID, 9063);
end

local MonsterSub = 0;

if EVENT == 8652 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 1, 850, 8293, NPC, 10, 8653);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 1, 850, 8294, NPC, 10, 158);
   end
end

if EVENT == 8653 then
    SelectMsg(UID, 1, 850, 8295, NPC, 22, 8654, 23, 8655);
end

if EVENT == 8654 then --¨ùo¢Òo¨öA
    ShowMap(UID, 505);
    SaveEvent(UID, 9064);
end

if EVENT == 8655 then --¡ÆAA©¥¨öA
    SelectMsg(UID, 1, 850, 8298, NPC, 4255, 8655);
    SaveEvent(UID, 9067);
end

-- 836©ö©ª ¨ùO¨ù¨¬ 0, 4©ö©ªAI ¡Æ©¡¢¯i

if EVENT == 8660 then
   SelectMsg(UID, 2, 850, 8296, NPC, 3002, 8661);
end

local MonsterSub = 0;

if EVENT == 8661 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 4, 850, 8297, NPC, 3018, 8662, 3019, 8668);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
    SelectMsg(UID, 2, 850, 8299, NPC, 10, 158);
   end
end

if EVENT == 8662 then --¨ùo¢Òo¨öA
    SaveEvent(UID, 9064);
end

if EVENT == 8668 then --¡ÆAA©¥¨öA
    SaveEvent(UID, 9067);
end

-- Ac¡¤a¢¬| ¢¥U ¢¬?¨úOA¡í¢Ò¡× ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa 

if EVENT == 8663 then
    SelectMsg(UID, 1, 850, 8300, NPC, 4080, 158);
    SaveEvent(UID, 9066);
end

-- 836AI 1©ö©ª 3©ö©ªAI ¡Æ©¡¢¯i 

local MonsterCount = 0;

if EVENT == 8665 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
    SelectMsg(UID, 2, 850, 8301, NPC, 10, 158);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
    SelectMsg(UID, 4, 850, 8302, NPC, 10, 8667, 27, 158);
   end
end

local Check;

if EVENT == 8667 then
   Check = CheckExchange(UID, 945)
   if  Check == true then
   RunExchange(UID, 945);
   SaveEvent(UID, 9059);
   SaveEvent(UID, 9065);
  else
   SelectMsg(UID, 2, 850, 8499, NPC, 10, 158);
  end	 	 
end

-------------------------------
--------¨¡¢ç¡¤¡í¨¡¢ç ¡íc©øE ©ø¢®---------
-------------------------------

-------------------------------
--------¢¯¢®AI¨ùC¨¡¢ç ¡íc©øE----------
-------------------------------

if EVENT == 9180 then
   SelectMsg(UID, 1, 829, 8274, NPC, 14, 9181);
end

if EVENT == 9181 then
   SaveEvent(UID, 9075);
end

local MonsterSub = 0;

if EVENT == 9182 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
	SelectMsg(UID, 2, 829, 8305, NPC, 10, 9190);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 2, 829, 8265, NPC, 10, 158);
	end
end

if EVENT == 9190 then
   SelectMsg(UID, 4, 829, 8306, NPC, 22, 9183, 23, 9184);
end

if EVENT == 9183 then
   SaveEvent(UID, 9076);
end

if EVENT == 9184 then
   SaveEvent(UID, 9079);
end

-- ¡íc©øE ¢¯I¡¤a¨öA ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa
if EVENT == 9185 then
   SelectMsg(UID, 1, 829, 8266, NPC, 3007, 158);
   SaveEvent(UID, 9078);
end

local MonsterCount = 0;

if EVENT == 9187 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
      SelectMsg(UID, 2, 829, 8267, NPC, 10, 9188);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
      SelectMsg(UID, 4, 829, 8268, NPC, 10, 9189, 27, 158);
   end
end

if EVENT == 9188 then
   ShowMap(UID, 546);
end

if EVENT == 9189 then
   Check = CheckExchange(UID, 947)
   if  Check == true then
   RunExchange(UID, 947);
   SaveEvent(UID, 9077);
   SaveEvent(UID, 9071);
   else
  Ret = 1;	
  end	 
end

-------------------------------
-------¢¯¢®AI¨ùC¨¡¢ç ¡íc©øE ©ø¢®--------
-------------------------------

-------------------------------
------¢¯e¨ú¨¡ ¡¾a¡íc ¢¥UAa ¡íc©øE------
-------------------------------

if EVENT == 9020 then
   SelectMsg(UID, 1, 871, 8274, NPC, 14, 9021);
end

if EVENT == 9021 then
   SaveEvent(UID, 9099);
end

local MonsterSub = 0;

if EVENT == 9022 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
	SelectMsg(UID, 2, 871, 8446, NPC, 10, 9030);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 2, 871, 8265, NPC, 10, 158);
	end
end

if EVENT == 9030 then
   SelectMsg(UID, 4, 871, 8447, NPC, 22, 9023, 23, 9024);
end

if EVENT == 9023 then
   SaveEvent(UID, 9100);
end

if EVENT == 9024 then
   SaveEvent(UID, 9103);
end

-- ¡íc©øE ¢¯I¡¤a¨öA ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa
if EVENT == 9025 then
   SelectMsg(UID, 1, 871, 8266, NPC, 3007, 158);
   SaveEvent(UID, 9102);
end

local MonsterCount = 0;

if EVENT == 9027 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
      SelectMsg(UID, 2, 871, 8267, NPC, 10, 9028);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
      SelectMsg(UID, 4, 871, 8451, NPC, 10, 9029, 27, 158);
   end
end

if EVENT == 9028 then
   ShowMap(UID, 586);
end

if EVENT == 9029 then
	Check = CheckExchange(UID, 1008)
	if  Check == true then  
   RunExchange(UID, 1008);
   SaveEvent(UID, 9101);
   SaveEvent(UID, 9095);
  else
   SelectMsg(UID, 2, 871, 8499, NPC, 10, 158);
  end	 
end

-------------------------------
----¢¯e¨ú¨¡ ¡¾a¡íc ¢¥UAa ¡íc©øE ©ø¢®-----
-------------------------------

------------------------------
------¢¯i¢¬¡ÌA¨Ï ¨¬i¡¤©öAI¥ìa ¡íc©øE-----
-------------------------------

if EVENT == 9040 then
   SelectMsg(UID, 1, 873, 8274, NPC, 14, 9041);
end

if EVENT == 9041 then
   SaveEvent(UID, 9111);
end

local MonsterSub = 0;

if EVENT == 9042 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
	SelectMsg(UID, 2, 873, 8456, NPC, 10, 9050);
   else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
 	SelectMsg(UID, 2, 873, 8265, NPC, 10, 158);
	end
end

if EVENT == 9050 then
   SelectMsg(UID, 4, 873, 8457, NPC, 22, 9043, 23, 9044);
end

if EVENT == 9043 then
   SaveEvent(UID, 9112);
end

if EVENT == 9044 then
   SaveEvent(UID, 9115);
end

-- ¡íc©øE ¢¯I¡¤a¨öA ¨öA¥ìa¢¯I ©öI¨ù¢¯AC ¥ìiAa
if EVENT == 9045 then
   SelectMsg(UID, 1, 873, 8266, NPC, 3007, 158);
   SaveEvent(UID, 9114);
end

local MonsterCount = 0;

if EVENT == 9047 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 250 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
      SelectMsg(UID, 2, 873, 8267, NPC, 10, 9048);
   else-- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
      SelectMsg(UID, 4, 873, 8268, NPC, 10, 9049, 27, 158);
   end
end

if EVENT == 9048 then
   ShowMap(UID, 550);
end

if EVENT == 9049 then
   Check = CheckExchange(UID, 1041)
   if  Check == true then
   RunExchange(UID, 1041);
   SaveEvent(UID, 9113);
   SaveEvent(UID, 9107);
   else
  Ret = 1;	
  end	 
end

-------------------------------
----¢¯i¢¬¡ÌA¨Ï ¨¬i¡¤©öAI¥ìa ¡íc©øE ©ø¢®----
-------------------------------

-------------------------------
------¥ì¡Í¡¤c¨¬n¨öA ¡íc©øE ¨öAAU-------
-------------------------------

if EVENT == 9080 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9150);
    EVENT = 9081
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9155);
    EVENT = 9081
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9160);
    EVENT = 9081
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9144);
     EVENT = 9081
   end
end

if EVENT == 9081 then
    SelectMsg(UID, 1, 877, 8464, NPC, 4080, 158);
end

local MonsterSub = 0;

if EVENT == 9082 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 2, 877, 8465, NPC, 10, 9085);
	else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
    SelectMsg(UID, 2, 877, 8265, NPC, 10, 158);
   end
end

if EVENT == 9085 then
    SelectMsg(UID, 4, 877, 8466, NPC, 22, 9083, 23, 9084);
end

if EVENT == 9083 then --¨ùo¢Òo¨öA
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9151);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9156);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9161);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9166);
   end
end

if EVENT == 9084 then
   SaveEvent(UID, 9154); -- ¡ÆAA©¥¨öA 
end


-- Ac¡¤a¢¬| ¢¥U ¢¬?¨úOA¡í¢Ò¡× ¨öA¥ìa ¢ÒC ¥ìiAa 

if EVENT == 9090 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9153);
    EVENT = 9091
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9158);
    EVENT = 9091
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9163);
    EVENT = 9091
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9168);
    EVENT = 9091
   end
end

if EVENT == 9091 then
    SelectMsg(UID, 1, 877, 8467, NPC, 3002, 158);
end

if EVENT == 9086 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 40 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
     SelectMsg(UID, 2, 877, 8468, NPC, 10, 9087);
    else -- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
     SelectMsg(UID, 4, 877, 8469, NPC, 10, 9088, 27, 158);
    end
end

if EVENT == 9087 then
   ShowMap(UID, 517);
end

local Check;

if EVENT == 9088 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 936)
        if  Check == true then        
        RunExchange(UID, 936);
        SaveEvent(UID, 9152);
        SaveEvent(UID, 9131);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 937)
        if  Check == true then        
        RunExchange(UID, 937);
        SaveEvent(UID, 9157);
        SaveEvent(UID, 9131);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 938)
        if  Check == true then        
        RunExchange(UID, 938);
        SaveEvent(UID, 9162);
        SaveEvent(UID, 9131);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 939)
        if  Check == true then        
        RunExchange(UID, 939);
        SaveEvent(UID, 9167);
        SaveEvent(UID, 9131);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
-------------------------------
-------¥ì¡Í¡¤c¨¬n¨öA ¡íc©øE ©ø¢®--------
-------------------------------

-------------------------------
--------¡íc¥ì¥ì ¡íc©øE ¨öAAU---------
-------------------------------

if EVENT == 9100 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9192);
    EVENT = 9101
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9197);
    EVENT = 9101
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9202);
    EVENT = 9101
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9207);
     EVENT = 9101
   end
end

if EVENT == 9101 then
    SelectMsg(UID, 1, 879, 8476, NPC, 4080, 158);
end

local MonsterSub = 0;

if EVENT == 9102 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 2, 879, 8477, NPC, 10, 9105);
	else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
    SelectMsg(UID, 2, 879, 8265, NPC, 10, 158);
   end
end

if EVENT == 9105 then
    SelectMsg(UID, 4, 879, 8478, NPC, 22, 9103, 23, 9104);
end

if EVENT == 9103 then --¨ùo¢Òo¨öA
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9193);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9198);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9203);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9208);
   end
end

if EVENT == 9104 then
   SaveEvent(UID, 9196); -- ¡ÆAA©¥¨öA 
end


-- Ac¡¤a¢¬| ¢¥U ¢¬?¨úOA¡í¢Ò¡× ¨öA¥ìa ¢ÒC ¥ìiAa 

if EVENT == 9110 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9195);
    EVENT = 9111
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9200);
    EVENT = 9111
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9205);
    EVENT = 9111
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9210);
    EVENT = 9111
   end
end

if EVENT == 9111 then
    SelectMsg(UID, 1, 879, 8467, NPC, 3002, 158);
end

if EVENT == 9106 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 40 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
     SelectMsg(UID, 2, 879, 8468, NPC, 10, 9107);
    else -- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
     SelectMsg(UID, 4, 879, 8479, NPC, 10, 9108, 27, 158);
    end
end

if EVENT == 9107 then
   ShowMap(UID, 552);
end

local Check;

if EVENT == 9108 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 916)
        if  Check == true then        
        RunExchange(UID, 916);
        SaveEvent(UID, 9194);
        SaveEvent(UID, 9173);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 917)
        if  Check == true then        
        RunExchange(UID, 917);
        SaveEvent(UID, 9199);
        SaveEvent(UID, 9173);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 918)
        if  Check == true then        
        RunExchange(UID, 918);
        SaveEvent(UID, 9204);
        SaveEvent(UID, 9173);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 919)
        if  Check == true then        
        RunExchange(UID, 919);
        SaveEvent(UID, 9209);
        SaveEvent(UID, 9173);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
-------------------------------
---------¡íc¥ì¥ì ¡íc©øE ©ø¢®----------
-------------------------------

-------------------------------
--------¨¡¢ç¡¤N ¡íc©øE ¨öAAU---------
-------------------------------

if EVENT == 9120 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9234);
    EVENT = 9121
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9239);
    EVENT = 9121
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9244);
    EVENT = 9121
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9249);
     EVENT = 9121
   end
end

if EVENT == 9121 then
    SelectMsg(UID, 1, 881, 8476, NPC, 4080, 158);
end

local MonsterSub = 0;

if EVENT == 9122 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 2, 881, 8483, NPC, 10, 9125);
	else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
    SelectMsg(UID, 2, 881, 8265, NPC, 10, 158);
   end
end

if EVENT == 9125 then
    SelectMsg(UID, 4, 881, 8484, NPC, 22, 9123, 23, 9124);
end

if EVENT == 9123 then --¨ùo¢Òo¨öA
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9235);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9240);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9245);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9250);
   end
end

if EVENT == 9124 then
   SaveEvent(UID, 9238); -- ¡ÆAA©¥¨öA 
end


-- Ac¡¤a¢¬| ¢¥U ¢¬?¨úOA¡í¢Ò¡× ¨öA¥ìa ¢ÒC ¥ìiAa 

if EVENT == 9130 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9237);
    EVENT = 9131
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9242);
    EVENT = 9131
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9247);
    EVENT = 9131
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9252);
    EVENT = 9131
   end
end

if EVENT == 9131 then
    SelectMsg(UID, 1, 881, 8467, NPC, 29, 158);
end

if EVENT == 9126 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 20 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
     SelectMsg(UID, 2, 881, 8468, NPC, 10, 9127);
    else -- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
     SelectMsg(UID, 4, 881, 8485, NPC, 10, 9128, 27, 158);
    end
end

if EVENT == 9127 then
   ShowMap(UID, 554);
end

local Check;

if EVENT == 9128 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 1048)
        if  Check == true then        
        RunExchange(UID, 1048);
        SaveEvent(UID, 9236);
        SaveEvent(UID, 9215);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 1049)
        if  Check == true then        
        RunExchange(UID, 1049);
        SaveEvent(UID, 9241);
        SaveEvent(UID, 9215);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 1050)
        if  Check == true then        
        RunExchange(UID, 1050);
        SaveEvent(UID, 9246);
        SaveEvent(UID, 9215);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1051)
        if  Check == true then        
        RunExchange(UID, 1051);
        SaveEvent(UID, 9251);
        SaveEvent(UID, 9215);
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
-------------------------------
---------¨¡¢ç¡¤N ¡íc©øE ©ø¢®----------
-------------------------------

-------------------------------
------¨ö¨¬A©¡¡Æn¡¤¨ö ¡íc©øE ¨öAAU-------
-------------------------------

if EVENT == 9160 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9318);
    EVENT = 9161
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9323);
    EVENT = 9161
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9328);
    EVENT = 9161
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9333);
     EVENT = 9161
   end
end

if EVENT == 9161 then
    SelectMsg(UID, 1, 885, 8476, NPC, 4080, 158);
end

local MonsterSub = 0;

if EVENT == 9162 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® ¨ú©ªA¡í¢Ò¡×
    SelectMsg(UID, 2, 885, 8489, NPC, 10, 9165);
	else-- ¢¬o¨ö¨¬AI ¨ù¡©¨¬e Au¨ö¨¬¨¡¢ç¡Æ¢® AOA¡í¢Ò¡×
    SelectMsg(UID, 2, 885, 8265, NPC, 10, 158);
   end
end

if EVENT == 9165 then
    SelectMsg(UID, 4, 885, 8490, NPC, 22, 9163, 23, 9164);
end

if EVENT == 9163 then --¨ùo¢Òo¨öA
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9319);
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9324);
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9329);
    elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9334);
   end
end

if EVENT == 9164 then
   SaveEvent(UID, 9322); -- ¡ÆAA©¥¨öA 
end


-- Ac¡¤a¢¬| ¢¥U ¢¬?¨úOA¡í¢Ò¡× ¨öA¥ìa ¢ÒC ¥ìiAa 

if EVENT == 9170 then
   Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    SaveEvent(UID, 9321);
    EVENT = 9171
    elseif Class == 2 or Class == 7 or Class == 8 then
    SaveEvent(UID, 9326);
    EVENT = 9171
    elseif Class == 3 or Class == 9 or Class == 10 then
    SaveEvent(UID, 9331);
    EVENT = 9171
   elseif Class == 4 or Class == 11 or Class == 12 then
    SaveEvent(UID, 9336);
    EVENT = 9171
   end
end

if EVENT == 9171 then
    SelectMsg(UID, 1, 885, 8467, NPC, 29, 158);
end

if EVENT == 9166 then
    MonsterCount  = CountMonsterQuestSub(UID, 32001);
    if  MonsterCount < 20 then -- ¡íc©øEAI ©ø©÷¨úOA¡í¢Ò¡×
     SelectMsg(UID, 2, 885, 8468, NPC, 10, 9167);
    else -- ¡íc©øEAI ¢¯I¡¤a ¥ìC¨úuA¡í¢Ò¡× 
     SelectMsg(UID, 4, 885, 8491, NPC, 10, 9168, 27, 158);
    end
end

if EVENT == 9167 then
   ShowMap(UID, 556);
end

local Check;

if EVENT == 9168 then
    Class = CheckClass(UID);
    if Class == 1 or Class == 5 or Class == 6 then
    Check = CheckExchange(UID, 997)
        if  Check == true then        
        RunExchange(UID, 997);
        SaveEvent(UID, 9320);
        SaveEvent(UID, 9299);        
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 2 or Class == 7 or Class == 8 then
    Check = CheckExchange(UID, 998)
        if  Check == true then        
        RunExchange(UID, 998);
        SaveEvent(UID, 9325);
        SaveEvent(UID, 9299);        
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 3 or Class == 9 or Class == 10 then
    Check = CheckExchange(UID, 999)
        if  Check == true then        
        RunExchange(UID, 999);
        SaveEvent(UID, 9330);
        SaveEvent(UID, 9299);        
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
    elseif Class == 4 or Class == 11 or Class == 12 then
    Check = CheckExchange(UID, 1000)
        if  Check == true then        
        RunExchange(UID, 1000);
        SaveEvent(UID, 9335);
        SaveEvent(UID, 9299);        
        ShowEffect(UID, 300391)
        else
        Ret = 1; 
        end  
  end	 
end
-------------------------------
--------¨ö¨¬A©¡¡Æn¡¤¨ö ¡íc©øE ©ø¢®-------
-------------------------------

return Ret;