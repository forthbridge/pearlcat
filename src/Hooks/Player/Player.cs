using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MoreSlugcats;
using RWCustom;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using VoidSea;
using static AbstractPhysicalObject;
using Random = UnityEngine.Random;

namespace Pearlcat;

public static partial class Hooks
{
    public static void ApplyPlayerHooks()
    {
        On.Player.Update += Player_Update;
        On.Player.checkInput += Player_checkInput;

        On.Player.Grabability += Player_Grabability;

        On.Player.Die += Player_Die;

        On.Creature.SuckedIntoShortCut += Creature_SuckedIntoShortCut;
        On.Creature.SpitOutOfShortCut += Creature_SpitOutOfShortCut;
        On.Creature.Violence += Creature_Violence;

        On.Player.SpearOnBack.Update += SpearOnBack_Update;

        _ = new Hook(
            typeof(Player).GetProperty(nameof(Player.VisibilityBonus), BindingFlags.Instance | BindingFlags.Public)?.GetGetMethod(),
            typeof(Hooks).GetMethod(nameof(GetPlayerVisibilityBonus), BindingFlags.Static | BindingFlags.Public)
        );

        On.Player.ctor += Player_ctor;
        On.Creature.Grasp.Release += Grasp_Release;

        On.VoidSea.VoidSeaScene.Update += VoidSeaScene_Update;

        On.Player.ThrownSpear += Player_ThrownSpear;
        On.Player.Stun += PlayerOnStun;

        try
        {
            IL.Creature.Update += Creature_Update;
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("Player Hooks IL Exception: \n" + e);
        }

    }


    private static void Player_ctor(On.Player.orig_ctor orig, Player self, AbstractCreature abstractCreature, World world)
    {
        orig(self, abstractCreature, world);

        if (!self.TryGetPearlcatModule(out _)) return;

        if (ModOptions.EnableBackSpear.Value)
        {
            self.spearOnBack ??= new Player.SpearOnBack(self);
        }
    }


    private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
    {
        if (self.TryGetPearlcatModule(out var playerModule) && self.spearOnBack != null)
        {
            playerModule.WasSpearOnBack = self.spearOnBack.HasASpear;
        }

        orig(self, eu);

        // zero G movement assist
        if (self.room != null && self.room.game.IsPearlcatStory() && self.room.roomSettings.name == "SS_AI" && self.room.gravity == 0.0f)
        {
            if (self.firstChunk.vel.magnitude < 7.5f)
            {
                self.firstChunk.vel += self.input[0].analogueDir * 0.7f;
            }

            if (self.input[0].analogueDir.magnitude < 0.05f)
            {
                self.firstChunk.vel *= 0.8f;
            }
        }

        if (playerModule == null) return;

        playerModule.BaseStats = self.Malnourished ? playerModule.MalnourishedStats : playerModule.NormalStats;

        var unblockedInput = playerModule.UnblockedInput;
        var allowInput = self.Consious && !self.inVoidSea && !self.Sleeping && self.controller == null;

        bool swapLeftInput = self.IsSwapLeftInput() && allowInput;
        bool swapRightInput = self.IsSwapRightInput() && allowInput;

        bool swapInput = self.IsSwapKeybindPressed() && allowInput;
        bool storeInput = self.IsStoreKeybindPressed(playerModule) && allowInput;

        bool agilityInput = self.IsAgilityKeybindPressed(playerModule) && allowInput;
        bool sentryInput = self.IsSentryKeybindPressed(playerModule) && allowInput;


        playerModule.BlockInput = false;

        if (swapLeftInput && !playerModule.WasSwapLeftInput)
        {
            self.SelectPreviousObject();
        }
        else if (swapRightInput && !playerModule.WasSwapRightInput)
        {
            self.SelectNextObject();
        }
        else if (swapInput)
        {
            playerModule.BlockInput = true;
            playerModule.ShowHUD(10);

            if (!playerModule.WasSwapped)
            {
                if (unblockedInput.x < -0.5f)
                {
                    self.SelectPreviousObject();
                    playerModule.WasSwapped = true;
                }
                else if (unblockedInput.x > 0.5f)
                {
                    self.SelectNextObject();
                    playerModule.WasSwapped = true;
                }
            }
            else if (Mathf.Abs(unblockedInput.x) < 0.5f)
            {
                playerModule.WasSwapped = false;
            }
        }

        UpdateAll(self, playerModule);

        playerModule.WasSwapLeftInput = swapLeftInput;
        playerModule.WasSwapRightInput = swapRightInput;
        playerModule.WasStoreInput = storeInput;
        playerModule.WasAgilityInput = agilityInput;
        playerModule.WasSentryInput = sentryInput;

        // LAG CAUSER
        if (playerModule.TextureUpdateTimer > self.TexUpdateInterval() && !ModOptions.DisableCosmetics.Value)
        {
            if ((playerModule.LastBodyColor != playerModule.BodyColor || playerModule.LastAccentColor != playerModule.AccentColor || playerModule.SetInvertTailColors != playerModule.CurrentlyInvertedTailColors))
            {
                playerModule.LoadTailTexture(playerModule.IsPearlpupAppearance ? "pearlpup_adulttail" : "tail");
                playerModule.LoadEarLTexture("ear_l");
                playerModule.LoadEarRTexture("ear_r");
            }

            playerModule.LastBodyColor = playerModule.BodyColor;
            playerModule.LastAccentColor = playerModule.AccentColor;

            playerModule.TextureUpdateTimer = 0;
        }
        else
        {
            playerModule.TextureUpdateTimer++;
        }



        if (self.canJump >= 5)
        {
            if (playerModule.GroundedTimer > 15)
            {
                playerModule.LastGroundedPos = self.firstChunk.pos;
            }
            else
            {
                playerModule.GroundedTimer++;
            }
        }
        else
        {
            playerModule.GroundedTimer = 0;
        }

        if (playerModule.ReviveCount > 0 && self.InDeathpit())
        {
            self.Die();
            self.SuperHardSetPosition(playerModule.LastGroundedPos);

            self.graphicsModule?.Reset();
            playerModule.FlyTimer = 60;

            var slugOnBack = self.slugOnBack?.slugcat;

            if (slugOnBack != null)
            {
                slugOnBack.SuperHardSetPosition(playerModule.LastGroundedPos);
                slugOnBack.graphicsModule?.Reset();
            }
        }
        if (playerModule.FlyTimer > 0)
        {
            playerModule.FlyTimer--;

            self.firstChunk.vel.x = self.input[0].x * 5.0f;
            self.firstChunk.vel.y = 6.0f;
        }

        if (self.inVoidSea || playerModule.Inventory.Any(x => x.Room != self.abstractCreature.Room))
        {
            self.AbstractizeInventory();
        }

        if (playerModule.PearlpupRef != null && playerModule.PearlpupRef.TryGetTarget(out var pup) && pup.room != null && pup.InDeathpit() && playerModule.ReviveCount > 0)
        {
            pup.SuperHardSetPosition(self.firstChunk.pos);
            
            pup.Die();
            pup.RevivePlayer();

            pup.graphicsModule.Reset();
            pup.Stun(40);

            playerModule.SetReviveCooldown(-1);
        }

        playerModule.LastRoom = self.abstractCreature.Room;

        if (playerModule.GraphicsResetCounter > 0)
        {
            playerModule.GraphicsResetCounter--;
            self.graphicsModule?.Reset();
        }
    }

    private static void UpdateAll(Player self, PlayerModule playerModule)
    {
        // Warp Fix
        if (self.room != null && playerModule.JustWarped)
        {
            self.GivePearls(playerModule);
            playerModule.LoadSaveData(self);

            Plugin.Logger.LogInfo("PEARLCAT WARP END");
            playerModule.JustWarped = false;
        }

        self.TryRealizeInventory(playerModule);

        UpdatePlayerOA(self, playerModule);
        UpdatePlayerDaze(self, playerModule);
        UpdatePostDeathInventory(self, playerModule);

        UpdateCombinedPOEffect(self, playerModule);
        ApplyCombinedPOEffect(self, playerModule);

        UpdateHUD(self, playerModule);
        UpdateSFX(self, playerModule);

        UpdateStoreRetrieveObject(self, playerModule);

        UpdateTryRevive(self, playerModule);

        if (self.room != null)
        {
            self.GivePearls(playerModule);
        }

        RefreshPearlpup(self, playerModule);

        UpdateAdultPearlpup(self, playerModule);
    }


    private const int POSSESSION_DELAY = 80;
    private const int REMOVE_HEART_DELAY = 320;

    private static void UpdateStoreRetrieveObject(Player self, PlayerModule playerModule)
    {
        if (self.inVoidSea) return;

        var storeObjectDelay = 30.0f;

        var storeInput = self.IsStoreKeybindPressed(playerModule);
        var toStore = self.grasps[0]?.grabbed;
        var isStoring = self.grasps[0]?.grabbed.abstractPhysicalObject.IsStorable() ?? false;

        if (isStoring && toStore == null) return;

        if (!isStoring && self.FreeHand() == -1) return;

        if (!isStoring && playerModule.ActiveObject == null) return;


        // Longer delay removing heart
        if (playerModule.ActiveObject.IsHeartPearl() && !isStoring)
        {
            storeObjectDelay = playerModule.PossessionTarget == null ? REMOVE_HEART_DELAY : POSSESSION_DELAY;
        }


        if (playerModule.StoreObjectTimer > storeObjectDelay)
        {
            if (isStoring && toStore != null)
            {
                self.StoreObject(toStore.abstractPhysicalObject, true);
            }
            else if (playerModule.ActiveObject != null)
            {
                if (playerModule.ActiveObject.IsHeartPearl())
                {
                    TryToRemoveHeart(self, playerModule, (DataPearl.AbstractDataPearl)playerModule.ActiveObject);
                }
                else
                {
                    self.room.PlaySound(Enums.Sounds.Pearlcat_PearlRetrieve, playerModule.ActiveObject.realizedObject.firstChunk);
                    self.RetrieveActiveObject();
                }
            }

            playerModule.StoreObjectTimer = -1;
        }


        if (storeInput)
        {
            if (playerModule.StoreObjectTimer >= 0)
            {
                if (isStoring || (playerModule.ActiveObject != null && playerModule.ActiveObject.TryGetPOModule(out var module) && !module.IsReturningSentry))
                {
                    playerModule.StoreObjectTimer++;

                    // every 5 frames
                    if (playerModule.StoreObjectTimer % 5 == 0)
                    {
                        if (isStoring)
                        {
                            var activeObjPos = self.GetActiveObjectPos();
                            toStore?.ConnectEffect(activeObjPos);                
                        }
                        else
                        {
                            var activeObj = playerModule.ActiveObject?.realizedObject;
                    
                            if (playerModule.ActiveObject?.TryGetPOModule(out module) == true)
                            {
                                if (!module.IsReturningSentry)
                                {
                                    activeObj.ConnectEffect(self.firstChunk.pos);
                                }

                                module.RemoveSentry(playerModule.ActiveObject);
                            }
                            else
                            {
                                activeObj.ConnectEffect(self.firstChunk.pos);
                            }
                        }
                    }


                    var heartRemovalStart = 40;

                    // trying to remove heart
                    if (playerModule.ActiveObject is DataPearl.AbstractDataPearl abstractHeart && abstractHeart.IsHeartPearl() && !isStoring)
                    {

                        // Removing the heart without any possessable creatures nearby
                        if (playerModule.PossessionTarget == null && playerModule.StoreObjectTimer > heartRemovalStart)
                        {
                            var heart = (DataPearl)abstractHeart.realizedObject;
                            var bigSparkFreq = (int)Custom.LerpMap(playerModule.StoreObjectTimer, heartRemovalStart, storeObjectDelay, 35, 1);
                            var heartBeatFreq = (int)Custom.LerpMap(playerModule.StoreObjectTimer, heartRemovalStart, storeObjectDelay, 35, 4);

                            if (playerModule.StoreObjectTimer % bigSparkFreq == 0)
                            {
                                var randVec = Custom.RNV() * Random.Range(150.0f, 250.0f);
                                self.room.ConnectEffect(heart.firstChunk.pos, heart.firstChunk.pos + randVec, Color.red, 8.0f, 40);
                                self.room.PlaySound(SoundID.Zapper_Zap, heart.firstChunk.pos, 0.8f, Random.Range(0.6f, 1.4f));
                            }

                            if (playerModule.StoreObjectTimer % heartBeatFreq == 0)
                            {
                                self.room.PlaySound(Enums.Sounds.Pearlcat_Heartbeat , heart.firstChunk.pos, Custom.LerpMap(playerModule.StoreObjectTimer, heartRemovalStart, storeObjectDelay, 0.45f, 1.0f), 1.0f);
                            }

                            if (playerModule.StoreObjectTimer % 10 == 0)
                            {
                                self.room.AddObject(new LightningMachine.Impact(heart.firstChunk.pos, 0.4f, Color.red));
                            }

                            if (playerModule.StoreObjectTimer % 30 == 0)
                            {
                                self.room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, heart.firstChunk.pos, 0.8f, 5.0f);
                                self.room.AddObject(new ExplosionSpikes(self.room, heart.firstChunk.pos, 5, 100.0f, 20.0f, 25.0f, 100.0f, Color.red));
                            }
                        }
                    }
                }
                
                playerModule.BlockInput = true;
                playerModule.ShowHUD(10);

                self.Blink(5);
            }
        }
        else
        {
            playerModule.StoreObjectTimer = 0;
        }
    }
    
    private static void UpdatePostDeathInventory(Player self, PlayerModule playerModule)
    {
        if (self.dead || playerModule.PostDeathInventory.Count == 0) return;

        for (int i = playerModule.PostDeathInventory.Count - 1; i >= 0; i--)
        {
            var item = playerModule.PostDeathInventory[i];
            playerModule.PostDeathInventory.RemoveAt(i);

            if (item.realizedObject == null) continue;

            if (item.realizedObject.room != self.room) continue;

            if (item.realizedObject.grabbedBy.Count > 0) continue;

            if (item.IsPlayerObject()) return;

            if (ModuleManager.PlayerObjectGraphicsData.TryGetValue(item, out var _))
                ModuleManager.PlayerObjectGraphicsData.Remove(item);

            self.StoreObject(item);
        }

        if (playerModule.PostDeathActiveObjectIndex != null)
        {
            ActivateObjectInStorage(self, (int)playerModule.PostDeathActiveObjectIndex);
        }

        playerModule.PostDeathActiveObjectIndex = null;
    }
    
    private static void UpdatePlayerOA(Player self, PlayerModule playerModule)
    {
        if (self.bodyMode == Player.BodyModeIndex.Stunned || self.bodyMode == Player.BodyModeIndex.Dead)
        {
            playerModule.CurrentObjectAnimation = new FreeFallOA(self);
        }
        else if (self.Sleeping || self.sleepCurlUp > 0.0f)
        {
            playerModule.CurrentObjectAnimation = new SleepOA(self);
        }
        else if (playerModule.CurrentObjectAnimation is SleepOA or FreeFallOA)
        {
            for (int i = 0; i < playerModule.Inventory.Count; i++)
            {
                var abstractObject = playerModule.Inventory[i];

                // just handle this before it gets out of hand
                if (i >= MaxPearlsWithEffects) break;
                
                if (abstractObject.TryGetSentry(out _)) continue;

                if (ModOptions.HidePearls.Value)
                {
                    if (playerModule.ActiveObject != abstractObject) continue;
                }

                abstractObject.realizedObject.ConnectEffect(((PlayerGraphics)self.graphicsModule).head.pos);
            }

            playerModule.PickObjectAnimation(self);
        }
        else
        {
            if (playerModule.CurrentObjectAnimation is SineWaveInterOA or SineWaveOA && self.firstChunk.vel.magnitude > 4.0f)
            {
                playerModule.PickObjectAnimation(self);
            }
        }

        if (playerModule.ObjectAnimationTimer > playerModule.ObjectAnimationDuration)
            playerModule.PickObjectAnimation(self);

        playerModule.CurrentObjectAnimation?.Update(self);
        playerModule.ObjectAnimationTimer++;
    }

        
    private static void UpdateSFX(Player self, PlayerModule playerModule)
    {
        // Outsider breaks looping SFX sometimes, this is safety
        try
        {
            playerModule.MenuCrackleLoop.Update();
            playerModule.MenuCrackleLoop.Volume = playerModule.HudFade;

            playerModule.ShieldHoldLoop.Update();
            playerModule.ShieldHoldLoop.Volume = playerModule.ShieldTimer > 0 ? 1.0f : 0.0f;

            // forced to fade out for some reason
            //if (playerModule.ActiveObject?.GetPOEffect().RMSong == true && self.firstChunk.vel.magnitude < 3.0f)
            //{
            //    if (self.room.game.manager.musicPlayer.song == null || self.room.game.manager.musicPlayer.song is not HalcyonSong)
            //        self.room.game.manager.musicPlayer.RequestHalcyonSong("NA_19 - Halcyon Memories");
            //}
            //else
            //{
            //    if (self.room.game.manager.musicPlayer != null && self.room.game.manager.musicPlayer.song != null && self.room.game.manager.musicPlayer.song is HalcyonSong)
            //        self.room.game.manager.musicPlayer.song.FadeOut(20f);
            //}
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError("Looping SFX Error, Outsider often likes to cause this:\n" + e);
        }
    }

    private static void UpdateHUD(Player self, PlayerModule playerModule)
    {
        if (playerModule.HudFadeTimer > 0)
        {
            playerModule.HudFadeTimer--;
            playerModule.HudFade = Mathf.Lerp(playerModule.HudFade, 1.0f, 0.1f);
        }
        else
        {
            playerModule.HudFadeTimer = 0;
            playerModule.HudFade = Mathf.Lerp(playerModule.HudFade, 0.0f, 0.05f);
        }
    }


    private static void UpdatePlayerDaze(Player self, PlayerModule playerModule)
    {
        var dazeDuration = 40;

        if (self.dead || self.bodyMode == Player.BodyModeIndex.Stunned || self.Sleeping)
        {
            playerModule.DazeTimer = dazeDuration;
        }

        if (playerModule.DazeTimer > 0)
        {
            playerModule.DazeTimer--;
        }
    }
    
    private static void UpdateTryRevive(Player self, PlayerModule playerModule)
    {
        bool shouldTryRevive = false;

        if (self.dead)
            shouldTryRevive = true;

        if (self.dangerGraspTime >= 60 && self.AI == null)
            shouldTryRevive = true;


        if (!shouldTryRevive) return;

        self.TryRevivePlayer(playerModule);
    }

    private static void RefreshPearlpup(Player self, PlayerModule playerModule)
    {
        if (!self.IsFirstPearlcat()) return;
        
        var save = self.abstractCreature.Room.world.game.GetMiscWorld();
        var miscProg = Utils.GetMiscProgression();

        miscProg.HasPearlpup = false;

        if (save != null)
        {
            save.HasPearlpupWithPlayer = false;
            save.HasPearlpupWithPlayerDeadOrAlive = false;
        }
        

        // Can get a reference to pearlpup (i.e. they're in the world somewhere)
        if (playerModule.PearlpupRef != null && playerModule.PearlpupRef.TryGetTarget(out var pup))
        {
            miscProg.HasPearlpup = !pup.dead && pup.abstractCreature.Room == self.abstractCreature.Room;

            if (save != null)
            {
                save.HasPearlpupWithPlayer = miscProg.HasPearlpup;

                save.HasPearlpupWithPlayerDeadOrAlive = pup.abstractCreature.Room == self.abstractCreature.Room;
            }

            return;
        }


        if (self.room == null) return;

        foreach (var roomObject in self.room.physicalObjects)
        {
            foreach (var physicalobject in roomObject)
            {
                if (physicalobject is not Player player) continue;

                if (player.IsPearlpup())
                {
                    playerModule.PearlpupRef = new(player);
                    return;
                }
            }
        }

        playerModule.PearlpupRef = null;
    }



    private static void Player_checkInput(On.Player.orig_checkInput orig, Player self)
    {
        orig(self);
        
        if (!self.TryGetPearlcatModule(out var playerModule)) return;
        
        var input = self.input[0];
        playerModule.UnblockedInput = input;

        if (playerModule.BlockInput)
        {
            input.x = 0;
            input.y = 0;
            input.analogueDir *= 0f;

            input.jmp = false;
            input.thrw = false;
            input.pckp = false;
        }

        self.input[0] = input;
    }

    private static void Player_Die(On.Player.orig_Die orig, Player self)
    {
        var wasDead = self.dead;

        orig(self);

        //Plugin.Logger.LogWarning(self.mainBodyChunk.pos);

        if (wasDead) return;

        if (!self.TryGetPearlcatModule(out var playerModule)) return;

        playerModule.ReviveTimer = 0;
        playerModule.ShieldTimer = 0;
        playerModule.SpearTimer = 0;

        playerModule.PostDeathActiveObjectIndex = playerModule.ActiveObjectIndex;

        self.room?.PlaySound(SoundID.Zapper_Zap, self.firstChunk.pos, 0.4f, 0.6f);
        self.room?.PlaySound(SoundID.Fire_Spear_Explode, self.firstChunk.pos, 0.7f, 0.6f);

        if (playerModule.ReviveCount <= 0 && playerModule.Inventory.Count > 0)
        {
            self.room?.AddObject(new ShockWave(self.firstChunk.pos, 30.0f, 0.4f, 5, false));
            self.room?.AddObject(new ExplosionSpikes(self.room, self.firstChunk.pos, 5, 20.0f, 10, 20.0f, 20.0f, Color.red));
        }

        for (int i = playerModule.Inventory.Count - 1; i >= 0; i--)
        {
            var abstractObject = playerModule.Inventory[i];

            RemoveFromInventory(self, abstractObject);

            playerModule.PostDeathInventory.Add(abstractObject);


            if (i < MaxPearlsWithEffects)
            {
                if (playerModule.ReviveCount <= 0)
                {
                    var randVec = Custom.RNV() * 150.0f;

                    self.room?.ConnectEffect(self.firstChunk.pos, self.firstChunk.pos + randVec, abstractObject.GetObjectColor(), 1.5f, 80);
                    DeathEffect(abstractObject.realizedObject);
                }
            }
        }
    }
    


    public delegate float orig_PlayerVisibilityBonus(Player self);
    public static float GetPlayerVisibilityBonus(orig_PlayerVisibilityBonus orig, Player self)
    {
        if (self.TryGetPearlcatModule(out var playerModule) || self.onBack?.TryGetPearlcatModule(out playerModule) == true
            || (self.grabbedBy.FirstOrDefault(x => x.grabber is Player)?.grabber as Player)?.TryGetPearlcatModule(out playerModule) == true)
            if (playerModule.CamoLerp > 0.5f)
                return -playerModule.CamoLerp;

        return orig(self);
    }

    private static void SpearOnBack_Update(On.Player.SpearOnBack.orig_Update orig, Player.SpearOnBack self, bool eu)
    {
        orig(self, eu);

        if (!self.owner.TryGetPearlcatModule(out var playerModule)) return;

        if (playerModule.ForceLockSpearOnBack)
            self.interactionLocked = true;
    }
    
    private static Player.ObjectGrabability Player_Grabability(On.Player.orig_Grabability orig, Player self, PhysicalObject obj)
    {
        var result = orig(self, obj);

        if (obj != null && obj.abstractPhysicalObject.IsPlayerObject())
            return Player.ObjectGrabability.CantGrab;

        return result;
    }

    
    private static void Grasp_Release(On.Creature.Grasp.orig_Release orig, Creature.Grasp self)
    {
        if (self.grabber is not Player player)
        {
            orig(self);
            return;
        }

        var inVoid = (player.inVoidSea || player.room?.roomSettings?.name == "SB_L01");

        if (inVoid && player.IsPearlcat() && self.grabbed?.firstChunk?.owner is Player pup && pup.IsPearlpup()) return;

        if (inVoid && player.IsPearlpup()) return;

        orig(self);
    }

    private static void Creature_Update(ILContext il)
    {
        var c = new ILCursor(il);

        c.GotoNext(MoveType.After,
            x => x.MatchLdstr("FORCE CREATURE RELEASE UNDER ROOM"));

        var dest = c.DefineLabel();

        c.GotoPrev(MoveType.After,
            x => x.MatchBle(out dest));

        c.Emit(OpCodes.Ldarg_0);
        c.EmitDelegate<Func<Creature, bool>>((self) =>
        {
            if (self is not Player player)
            {
                return false;
            }

            var inVoid = (player.inVoidSea || player.room?.roomSettings?.name == "SB_L01");

            if (inVoid && player.IsPearlpup())
            {
                // Plugin.Logger.LogWarning("PREVENTED PEARLPUP GRASP RELEASE");
                return true;
            }

            // Plugin.Logger.LogWarning("DID NOT PREVENT RELEASE");
            return false;
        });

        c.Emit(OpCodes.Brtrue, dest);

        // Plugin.Logger.LogWarning(c.Context);
    }

    private static void Creature_SpitOutOfShortCut(On.Creature.orig_SpitOutOfShortCut orig, Creature self, IntVector2 pos, Room newRoom, bool spitOutAllSticks)
    {
        orig(self, pos, newRoom, spitOutAllSticks);

        if (self is Player p && p.TryGetPearlcatModule(out var mod))
            mod.LastGroundedPos = p.firstChunk.pos;

        foreach (var playerModule in self.abstractCreature.Room.world.game.GetAllPlayerData())
        {
            foreach (var item in playerModule.Inventory)
            {
                if (!item.TryGetSentry(out var sentry)) continue;

                if (!item.TryGetPOModule(out var module)) continue;

                if (module.CooldownTimer != 0 && sentry.ShieldTimer <= 0) continue;

                var effect = item.GetPOEffect();
                if (effect.MajorEffect != POEffect.MajorEffectType.SHIELD) continue;

                if (!sentry.OwnerRef.TryGetTarget(out var owner)) continue;

                if (owner.realizedObject == null) continue;

                if (!Custom.DistLess(owner.realizedObject.firstChunk.pos, newRoom.MiddleOfTile(pos), 75.0f)) continue;

                if (sentry.ShieldTimer <= 0)
                    sentry.ShieldTimer = ModOptions.ShieldDuration.Value * 3.0f;

                owner.realizedObject.room?.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, owner.realizedObject.firstChunk, false, 1.0f, 0.7f);
                owner.realizedObject.room?.DeflectEffect(newRoom.MiddleOfTile(pos));

                self.SuckedIntoShortCut(pos, false);
            }
        }
    }

    private static void Creature_SuckedIntoShortCut(On.Creature.orig_SuckedIntoShortCut orig, Creature self, IntVector2 entrancePos, bool carriedByOther)
    {
        if (self is Player player)
        {
            var sameRoom = false;
            
            if (player.TryGetPearlcatModule(out PlayerModule? playerModule) || player.slugOnBack?.slugcat?.TryGetPearlcatModule(out playerModule) == true)
            {
                sameRoom = player.abstractCreature.Room == playerModule.LastRoom;
            }

            player.AbstractizeInventory(sameRoom);
            player.slugOnBack?.slugcat?.AbstractizeInventory(sameRoom);
        }

        orig(self, entrancePos, carriedByOther);
    }

    private static void Creature_Violence(On.Creature.orig_Violence orig, Creature self, BodyChunk source, Vector2? directionAndMomentum, BodyChunk hitChunk, PhysicalObject.Appendage.Pos hitAppendage, Creature.DamageType type, float damage, float stunBonus)
    {
        // sin number 2
        if (self is Player player && player.TryGetPearlcatModule(out var playerModule))
        {
            bool shouldShield = playerModule.ShieldActive;
            var attacker = source?.owner;

            if (attacker is JetFish)
                shouldShield = false;

            if (attacker is Cicada)
                shouldShield = false;

            if (attacker is Centipede centipede && centipede.Small)
                shouldShield = false;

            if (damage <= 0.1f)
                shouldShield = false;

            if (shouldShield)
            {
                playerModule.ActivateVisualShield();
                return;
            }
        }

        orig(self, source, directionAndMomentum, hitChunk, hitAppendage, type, damage, stunBonus);
    }
    
    
    private static void VoidSeaScene_Update(On.VoidSea.VoidSeaScene.orig_Update orig, VoidSeaScene self, bool eu)
    {
        orig(self, eu);

        if (!self.room.game.IsPearlcatStory()) return;

        foreach (var obj in self.room.updateList)
        {
            if (obj is not Player player) continue;

            if (player.inVoidSea) continue;

            if (!player.IsPearlpup()) continue;

            player.inVoidSea = true;
            self.UpdatePlayerInVoidSea(player);
        }

        if (self.deepDivePhase == VoidSeaScene.DeepDivePhase.EggScenario)
        {
            var miscProg = Utils.GetMiscProgression();
            var miscWorld = self.room.abstractRoom.world.game.GetMiscWorld();

            miscProg.JustAscended = true;
            
            if (miscWorld?.HasPearlpupWithPlayer == true)
            {
                miscProg.AscendedWithPup = true;
            }

            SlugBase.Assets.CustomScene.SetSelectMenuScene(self.room.game.GetStorySession.saveState, Enums.Scenes.Slugcat_Pearlcat_Ascended);
        }
    }



    private static void UpdateAdultPearlpup(Player self, PlayerModule playerModule)
    {
        if (!playerModule.IsAdultPearlpup) return;

        var hasHeart = playerModule.Inventory.Any(x => x is DataPearl.AbstractDataPearl dataPearl && dataPearl.IsHeartPearl()) || playerModule.PostDeathInventory.Any(x => x is DataPearl.AbstractDataPearl dataPearl && dataPearl.IsHeartPearl());
        var room = self.room;

        if (room != null && !self.dead && !hasHeart)
        {
            var pearl = new DataPearl.AbstractDataPearl(self.room.world, AbstractObjectType.DataPearl, null, self.abstractPhysicalObject.pos, room.game.GetNewID(), -1, -1, null, Enums.Pearls.Heart_Pearlpup);
            self.StoreObject(pearl, overrideLimit: true);
        }


        if (playerModule.IsPossessingCreature)
        {
            playerModule.PossessionTarget = null;

            AbstractCreature? possessedCreature = null;
            var shouldReleasePossession = playerModule.PossessedCreature == null || !playerModule.PossessedCreature.TryGetTarget(out possessedCreature);

            if (possessedCreature != null)
            {
                if (possessedCreature.realizedCreature == null || possessedCreature.realizedCreature.dead)
                {
                    shouldReleasePossession = true;
                }
            }

            if (shouldReleasePossession || possessedCreature == null)
            {
                ReleasePossession(self, playerModule);
                return;
            }


            playerModule.BlockInput = true;
            possessedCreature.controlled = true;

            if (possessedCreature.realizedCreature?.room == null)
            {
                self.SuckedIntoShortCut(possessedCreature.pos.Tile, false);
            }
            else
            {
                if (room == null)
                {
                    self.abstractCreature.ChangeRooms(possessedCreature.pos);
                    self.abstractCreature.RealizeInRoom();
                }

                self.ChangeCollisionLayer(0);
                self.SuperHardSetPosition(possessedCreature.realizedCreature.firstChunk.pos);
               
                foreach (var chunk in self.bodyChunks)
                {
                    chunk.vel = Vector2.zero; 
                }
            }
        }
        else
        {
            if (room == null) return;

            if (!playerModule.ActiveObject.IsHeartPearl())
            {
                playerModule.PossessionTarget = null;
                return;
            }

            const float possessionMaxDist = 400.0f;
            const float possessionLostDist = 400.0f;

            // search for target
            if (playerModule.PossessionTarget == null || !playerModule.PossessionTarget.TryGetTarget(out var target))
            {
                Creature? bestTarget = null;
                var shortestDist = float.MaxValue;

                foreach (var roomObject in room.physicalObjects)
                {
                    foreach (var physicalObject in roomObject)
                    {
                        if (physicalObject is Player) continue;

                        if (physicalObject is not Creature creature) continue;

                        if (creature.dead) continue;

                        if (creature.abstractCreature.controlled) continue;


                        var dist = Custom.Dist(creature.mainBodyChunk.pos, self.firstChunk.pos);

                        if (dist > possessionMaxDist) continue;

                        if (dist > shortestDist) continue;

                        if (!self.room.VisualContact(self.mainBodyChunk.pos, creature.mainBodyChunk.pos)) continue;

                        shortestDist = dist;
                        bestTarget = creature;
                    }
                }

                if (bestTarget != null)
                {
                    playerModule.PossessionTarget = new(bestTarget);
                }
            }
            else
            {
                // ensure target is still valid
                bool invalidTarget = false;

                if (!Custom.DistLess(target.mainBodyChunk.pos, self.mainBodyChunk.pos, possessionLostDist))
                    invalidTarget = true;

                if (target.room != self.room)
                    invalidTarget = true;

                if (target.dead)
                    invalidTarget = true;

                if (!self.room.VisualContact(self.mainBodyChunk.pos, target.mainBodyChunk.pos))
                    invalidTarget = true;


                if (invalidTarget)
                {
                    playerModule.PossessionTarget = null;
                    playerModule.StoreObjectTimer = 0;
                }
            }
        }
    }

    private static void TryToRemoveHeart(Player self, PlayerModule playerModule, DataPearl.AbstractDataPearl dataPearl)
    {
        if (self.room == null) return;

        if (playerModule.PossessionTarget?.TryGetTarget(out var target) == true)
        {
            self. PossessCreature(playerModule, target);
        }
        else
        {
            var room = self.room;
            var pos = self.firstChunk.pos;

            self.firstChunk.vel.y += 20.0f;

            room.AddObject(new Explosion.ExplosionLight(pos, 100.0f, 1.0f, 3, Color.red));
            room.AddObject(new ShockWave(pos, 250.0f, 0.07f, 6, false));

            room.AddObject(new ExplosionSpikes(room, pos, 5, 100.0f, 20.0f, 25.0f, 100.0f, Color.red));
            room.AddObject(new LightningMachine.Impact(pos, 2.0f, Color.red, true));

            for (int i = 0; i < 4; i++)
            {
                var randVec = Custom.RNV() * 150.0f;
                room.ConnectEffect(pos, pos + randVec, Color.red, 1.5f, 80);
            }

            room.PlaySound(SoundID.Fire_Spear_Explode, pos, 1.2f, 0.8f);
            room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, pos, 1.0f, 1.0f);

            self.Die();
        }
    }

    private static void PossessCreature(this Player self, PlayerModule playerModule, Creature target)
    {
        playerModule.PossessionTarget = null;
        playerModule.PossessedCreature = new(target.abstractCreature);

        self.LoseAllGrasps();
        self.Stun(10);

        var room = self.room;
        var pos = self.firstChunk.pos;

        for (int i = 0; i < 5; i++)
        {
            room.AddObject(new NeuronSpark(pos));
        }

        room.AddObject(new Explosion.ExplosionLight(pos, 150f, 1f, 8, Color.white));
        room.AddObject(new ShockWave(pos, 60f, 0.1f, 8, false));

        room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, pos, 0.6f, 2.5f + Random.value * 0.5f);
    }

    private static void ReleasePossession(Player self, PlayerModule playerModule)
    {
        if (playerModule.PossessedCreature?.TryGetTarget(out var creature) == true)
        {
            creature.controlled = false;
        }
        else
        {
            return;
        }

        playerModule.PossessedCreature = null;
        playerModule.PossessionTarget = null;

        var room = self.room;
        var pos = self.firstChunk.pos;

        for (int i = 0; i < 5; i++)
        {
            room.AddObject(new Spark(pos, Custom.RNV(), Color.white, null, 16, 24));
        }

        room.AddObject(new Explosion.ExplosionSmoke(pos, Custom.RNV() * 2f * Random.value, 1f));

        room.AddObject(new Explosion.ExplosionLight(pos, 150f, 1f, 8, Color.white));
        room.AddObject(new ShockWave(pos, 60f, 0.3f, 16, false));

        room.PlaySound(SoundID.Bomb_Explode, pos, 0.5f, 1.2f);
        room.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, pos, 0.6f, 0.3f + Random.value * 0.2f);

        self.InitiateGraphicsModule();
        self.graphicsModule?.Reset();
    }



    private static void Player_ThrownSpear(On.Player.orig_ThrownSpear orig, Player self, Spear spear)
    {
        orig(self, spear);

        if (spear.abstractSpear.TryGetSpearModule(out var spearModule))
        {
            spearModule.ReturnTimer = -1;
            spearModule.ThrownByPlayer = new(self);
        }
    }


    private static void PlayerOnStun(On.Player.orig_Stun orig, Player self, int st)
    {
        if (self.TryGetPearlcatModule(out var playerModule))
        {
            if (playerModule.IsPossessingCreature) return;
        }

        orig(self, st);
    }
}
