using CWStuff;
using UnityEngine;

namespace Pearlcat;

public static class CWIntegration
{
    public static void Init()
    {
        On.SSOracleBehavior.SpecialEvent += SSOracleBehavior_SpecialEvent;

        CWConversation.OnAddEvents += CWConversation_OnAddEvents;
    }

    private static void CWConversation_OnAddEvents(CWConversation self, ref bool runOriginalCode)
    {
        var room = self.owner?.oracle?.room;

        if (room == null || room.game.IsPearlcatStory()) return;


        var miscProg = Utils.GetMiscProgression();
        var miscWorld = room.game.GetMiscWorld();

        if (miscWorld == null) return;

        if (!CWOracleHooks.WorldSaveData.TryGetValue(room.game.GetStorySession.saveState.miscWorldSaveData, out var cwSaveData)) return;


        runOriginalCode = false;

        var rand = Random.Range(0, 100000);

        
        if (miscProg.HasTrueEnding)
        {
            switch (miscWorld.CWMeetCount)
            {
                case 1:
                    CWConversation.CWEventsFromFile(self, "Pearlcat_FirstEncounter_TrueEnd");
                    break;

                case 2:
                    CWConversation.CWEventsFromFile(self, "Pearlcat_SecondEncounter_TrueEnd");
                    break;

                default:
                    CWConversation.CWEventsFromFile(self, "Pearlcat_RandomGreeting_TrueEnd", false, null, true, rand);
                    break;
            }
        }
        else
        {
            if (miscWorld.HasPearlpupWithPlayer)
            {
                if (miscProg.IsPearlpupSick)
                {
                    switch (miscWorld.CWMeetSickCount)
                    {
                        case 1:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_FirstEncounter_SickPup");
                            break;

                        case 2:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_SecondEncounter_SickPup");
                            break;

                        default:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_RandomGreeting_SickPup", false, null, true, rand);
                            break;
                    }

                    miscWorld.CWMeetSickCount++;
                }
                else
                {
                    switch (miscWorld.CWMeetCount)
                    {
                        case 1:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_FirstEncounter_HasPup");
                            break;

                        case 2:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_SecondEncounter_HasPup");
                            break;

                        default:
                            CWConversation.CWEventsFromFile(self, "Pearlcat_RandomGreeting_HasPup", false, null, true, rand);
                            break;
                    }
                }
            }
            else
            {
                switch (miscWorld.CWMeetCount)
                {
                    case 1:
                        CWConversation.CWEventsFromFile(self, "Pearlcat_FirstEncounter_NoPup");
                        break;

                    case 2:
                        CWConversation.CWEventsFromFile(self, "Pearlcat_RandomGreeting_NoPup", false, null, true, rand);
                        break;

                    default:
                        CWConversation.CWEventsFromFile(self, "Pearlcat_SecondEncounter");
                        break;
                }
            }
        }

        miscWorld.CWMeetCount++;
    }

    private static void SSOracleBehavior_SpecialEvent(On.SSOracleBehavior.orig_SpecialEvent orig, SSOracleBehavior self, string eventName)
    {
        orig(self, eventName);

        if (eventName == "PEARLCAT_GIVE_CW")
        {
            GiveCWPearl(self.oracle);
        }
    }

    private static void GiveCWPearl(Oracle oracle, bool withEffect = true)
    {
        var miscWorld = oracle.room.game.GetMiscWorld();

        if (miscWorld is null) return;


        if (miscWorld.CWGavePearl) return;

        miscWorld.CWGavePearl = true;


        foreach (var roomObject in oracle.room.physicalObjects)
        {
            foreach (var physicalObject in roomObject)
            {
                if (physicalObject is not Player player) continue;

                if (!player.TryGetPearlcatModule(out var playerModule)) continue;


                var abstractPearl = new DataPearl.AbstractDataPearl(oracle.room.world, AbstractPhysicalObject.AbstractObjectType.DataPearl, null, player.abstractCreature.pos, oracle.room.game.GetNewID(), -1, -1, null, Enums.Pearls.CW_Pearlcat);

                if (playerModule.Inventory.Count >= ModOptions.MaxPearlCount.Value)
                {
                    abstractPearl.pos = player.abstractCreature.pos;
                    oracle.room.abstractRoom.AddEntity(abstractPearl);
                    abstractPearl.RealizeInRoom();

                    var freeHand = player.FreeHand();

                    if (freeHand > -1)
                    {
                        player.SlugcatGrab(abstractPearl.realizedObject, freeHand);
                    }
                }
                else
                {
                    player.StoreObject(abstractPearl);

                    if (withEffect)
                    {
                        playerModule.ShowHUD(120);
                    }
                }

                if (withEffect)
                {
                    oracle.room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, physicalObject.firstChunk, false, 1.5f, 0.5f);
                }
            }
        }
    }
}
