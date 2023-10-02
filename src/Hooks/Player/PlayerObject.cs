﻿using System.Collections.Generic;
using static DataPearl.AbstractDataPearl;

namespace Pearlcat;

public static partial class Hooks
{
    // Realization & Abstraction
    public static void TryRealizeInventory(this Player self, PlayerModule playerModule)
    {
        if (self.room == null) return;

        if (playerModule.JustWarped) return;

        if (self.inVoidSea) return;


        foreach (var abstractObject in playerModule.Inventory)
        {
            if (abstractObject == null) continue;

            if (abstractObject.realizedObject != null)
            {
                abstractObject.MarkAsPlayerObject();
                continue;
            }
            
            abstractObject.pos = self.abstractCreature.pos;   
            self.room.abstractRoom.AddEntity(abstractObject);
            
            abstractObject.RealizeInRoom();

            abstractObject.MarkAsPlayerObject();
            abstractObject.realizedObject?.RealizedEffect();
        }
        
        //self.room.PlaySound(Enums.Sounds.Pearlcat_PearlRealize, self.firstChunk.pos);
    }

    public static void AbstractizeInventory(this Player self, bool excludeSentries = false)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;


        foreach (var abstractObject in playerModule.Inventory)
        {
            if (abstractObject.realizedObject == null) continue;

            if (abstractObject.TryGetSentry(out _) && excludeSentries) continue;

            if (abstractObject.TryGetModule(out var module))
                module.RemoveSentry(abstractObject);

            AbstractedEffect(abstractObject.realizedObject);
            abstractObject.Abstractize(abstractObject.pos);

        }

        //if (playerModule.Inventory.Count > 0)
        //    self.room.PlaySound(Enums.Sounds.Pearlcat_PearlAbstract, self.firstChunk.pos);
    }


    // Storage & Removal
    public static void StoreObject(this Player self, AbstractPhysicalObject abstractObject, bool fromGrasp = false, bool overrideLimit = false)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        var save = self.abstractCreature.world.game.GetMiscWorld();
    
        if (playerModule.Inventory.Count >= ModOptions.MaxPearlCount.Value && !overrideLimit)
        {
            if (save?.ShownFullInventoryTutorial == false && !ModOptions.DisableTutorials.Value)
            {
                var t = self.abstractCreature.world.game.rainWorld.inGameTranslator;

                save.ShownFullInventoryTutorial = true;
                self.abstractCreature.world.game.AddTextPrompt(t.Translate("Storage limit reached (") + ModOptions.MaxPearlCount.Value + t.Translate("): swap out a pearl, or change the limit in the Remix options"), 40, 600);
            }

            self.room?.PlaySound(SoundID.MENU_Error_Ping, self.firstChunk, false, 2.0f, 1.0f);
            return;
        }

        if (fromGrasp)
        {
            self.room?.PlaySound(Enums.Sounds.Pearlcat_PearlStore, self.firstChunk);
            self.ReleaseGrasp(0);
        }

        if (abstractObject is AbstractSpear spear && spear.TryGetSpearModule(out var spearModule))
        {
            abstractObject.destroyOnAbstraction = true;
            abstractObject.Abstractize(abstractObject.pos);

            ExtEnumBase.TryParse(typeof(DataPearlType), spearModule.PearlType, false, out var type);

            abstractObject = new DataPearl.AbstractDataPearl(self.abstractCreature.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null,
                new(self.abstractCreature.Room.index, -1, -1, 0), self.abstractCreature.world.game.GetNewID(), -1, -1, null, type as DataPearlType ?? DataPearlType.Misc);

            self.room?.PlaySound(Enums.Sounds.Pearlcat_PearlStore, self.firstChunk, false, 1.0f, 0.5f);
            self.room?.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, self.firstChunk, false, 1.0f, 3.5f);
        }

        self.AddToInventory(abstractObject);

        int targetIndex = playerModule.ActiveObjectIndex ?? 0;
        self.ActivateObjectInStorage(targetIndex);
        
        playerModule.PickObjectAnimation(self);
        playerModule.ShowHUD(30);

        self.UpdateInventorySaveData(playerModule);

        if (save?.ShownSpearCreationTutorial == false && abstractObject.GetPOEffect().MajorEffect == POEffect.MajorEffectType.SPEAR_CREATION && !ModOptions.DisableTutorials.Value)
        {
            save.ShownSpearCreationTutorial = true;

            var t = self.abstractCreature.world.game.rainWorld.inGameTranslator;

            if (ModOptions.CustomSpearKeybind.Value)
                self.abstractCreature.world.game.AddTextPrompt(
                    t.Translate("Hold (") + ModOptions.AbilityKeybindKeyboard.Value + t.Translate(") or (") + ModOptions.AbilityKeybindPlayer1.Value.GetDisplayName() + t.Translate(") with an active common pearl to convert it into a pearl spear"), 0, 800);

            else
                self.abstractCreature.world.game.AddTextPrompt("Hold (GRAB) with an active common pearl to convert it into a pearl spear", 0, 800);
        }
    }
    
    public static void RetrieveActiveObject(this Player self)
    {
        if (self.FreeHand() <= -1) return;

        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        var activeObject = playerModule.ActiveObject;
        if (activeObject == null) return;

        self.RemoveFromInventory(activeObject);
        self.SlugcatGrab(activeObject.realizedObject, self.FreeHand());

        playerModule.PickObjectAnimation(self);
        playerModule.ShowHUD(30);

        if (playerModule.ActiveObject == null && playerModule.Inventory.Count > 0)
        {
            var targetIndex = playerModule.ActiveObjectIndex ?? 0;

            if (playerModule.ActiveObjectIndex != null)
            {
                targetIndex = playerModule.ActiveObjectIndex.Value - 1;

                if (targetIndex < 0)
                    targetIndex = playerModule.Inventory.Count - 1;
            }

            self.ActivateObjectInStorage(targetIndex);
        }

        self.UpdateInventorySaveData(playerModule);
    }


    public static void AddToInventory(this Player self, AbstractPhysicalObject abstractObject, bool addToEnd = false)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        int targetIndex = playerModule.ActiveObjectIndex ?? 0;

        if (addToEnd)
            playerModule.Inventory.Add(abstractObject);
       
        else
            playerModule.Inventory.Insert(targetIndex, abstractObject);
        
        abstractObject.MarkAsPlayerObject();
    }
    
    public static void RemoveFromInventory(this Player self, AbstractPhysicalObject abstractObject)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        if (!playerModule.Inventory.Contains(abstractObject)) return;

        playerModule.Inventory.Remove(abstractObject);
        abstractObject.ClearAsPlayerObject();

        if (abstractObject.TryGetAddon(out var addon))
            addon.Destroy();

        if (abstractObject.TryGetModule(out var module))
            module.RemoveSentry(abstractObject);

        if (playerModule.Inventory.Count == 0)
            playerModule.ActiveObjectIndex = null;

        InventoryHUD.Symbols.Remove(abstractObject);
    }


    // Selection
    public static void SelectNextObject(this Player self)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;


        if (playerModule.ActiveObjectIndex == null) return;

        if (playerModule.Inventory.Count <= 1) return;


        int targetIndex = (int)playerModule.ActiveObjectIndex + 1;

        if (targetIndex >= playerModule.Inventory.Count)
            targetIndex = 0;

        self.ActivateObjectInStorage(targetIndex);
    }

    public static void SelectPreviousObject(this Player self)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;


        if (playerModule.ActiveObjectIndex == null) return;

        if (playerModule.Inventory.Count <= 1) return;


        int targetIndex = (int)playerModule.ActiveObjectIndex - 1;

        if (targetIndex < 0)
            targetIndex = playerModule.Inventory.Count - 1;

        self.ActivateObjectInStorage(targetIndex);
    }

    public static void ActivateObjectInStorage(this Player self, int objectIndex)
    {
        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        if (objectIndex < 0 ||  objectIndex >= playerModule.Inventory.Count) return;

        if (objectIndex == playerModule.ActiveObjectIndex) return;

        var oldObject = playerModule.ActiveObject?.realizedObject;
        playerModule.ActiveObjectIndex = objectIndex;
        var newObject = playerModule.ActiveObject?.realizedObject;

        oldObject.SwapEffect(newObject);

        playerModule.ShowHUD(60);
        self.PlayHUDSound(Enums.Sounds.Pearlcat_PearlScroll);
        
        var save = self.room.game.GetMiscWorld();

        if (save != null)
            save.ActiveObjectIndex[self.playerState.playerNumber] = objectIndex;

        if (self.graphicsModule is not PlayerGraphics pGraphics || newObject == null) return;

        //player.room.PlaySound(Enums.Sounds.Pearlcat_PearlEquip, newObject.firstChunk.pos);
        pGraphics.LookAtPoint(newObject.firstChunk.pos, 1.0f);

        self.UpdateInventorySaveData(playerModule);
    }


    // Save
    public static void UpdateInventorySaveData(this Player self, PlayerModule playerModule)
    {
        if (ModOptions.InventoryOverride.Value) return;

        var save = self.room.game.GetMiscWorld();

        if (save == null) return;

        List<string> inventory = new();

        foreach (var item in playerModule.Inventory)
            inventory.Add(item.ToString());


        save.Inventory[self.playerState.playerNumber] = inventory;

        if (playerModule.Inventory.Count == 0)
        {
            playerModule.ActiveObjectIndex = null;
            save.ActiveObjectIndex[self.playerState.playerNumber] = null;
        }
    }
}
