using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ThoraxLab.Learning;

class Program
{
    static int assertions;
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static ObjectiveDefinition Objective(string id, ActionKind kind = ActionKind.Select) => new ObjectiveDefinition { id = id, targetId = id, action = kind };
    static InteractionAction Hit(string id, ActionKind kind = ActionKind.Select) => new InteractionAction { targetId = id, kind = kind, contactVerified = true };
    static AssessmentEngine Engine(params ObjectiveDefinition[] objectives) => new AssessmentEngine(new ScenarioVariant { objectives = objectives, ordered = true }, new AttemptRecord { userId = "account-a", contentVersion = LearningContent.CurrentVersion, phase = SessionPhase.Assessment });

    static void CheckCheckpointReconciliation()
    {
        var progress=new ProgressSnapshot{userId="account-a"};
        ActivityCheckpoint Checkpoint(int count, int minute=0)
        {
            var attempt=new AttemptRecord{id="same-attempt",userId="account-a",deviceId="device-a",scenarioId="case",phase=SessionPhase.Practice};
            for(int i=0;i<count;i++){var action=Hit("step-"+i);action.id="event-"+i;attempt.actions.Add(action);}
            return new ActivityCheckpoint{attemptId=attempt.id,scenarioId=attempt.scenarioId,phase=attempt.phase,attempt=attempt,savedUtc=new DateTimeOffset(2026,9,6,10,minute,0,TimeSpan.Zero).ToString("o")};
        }
        var first=Checkpoint(1);
        Check(ProgressRules.MergeCheckpoint(progress,first),"First remote checkpoint rejected.");
        Check(ProgressRules.MergeCheckpoint(progress,Checkpoint(3,3))&&progress.attempts.Single().actions.Count==3,"Newer remote checkpoint left resume on step one.");
        Check(!ProgressRules.MergeCheckpoint(progress,first)&&progress.attempts.Single().actions.Count==3,"Stale checkpoint rolled back safe progress.");
        Check(!ProgressRules.MergeCheckpoint(progress,Checkpoint(3,3)),"Repeated checkpoint was reapplied.");
        Check(!ProgressRules.MergeCheckpoint(progress,Checkpoint(4,4),"same-attempt")&&progress.attempts.Single().actions.Count==3,"Active local work was overwritten by an in-flight reply.");
        Check(ProgressRules.MergeCheckpoint(progress,Checkpoint(4,4)),"Same-device backup recovery could not import newer inactive cloud work.");
        var divergent=Checkpoint(5,5);divergent.attempt.actions[0].id="changed-history";
        Check(!ProgressRules.MergeCheckpoint(progress,divergent),"A non-append checkpoint rewrote recorded errors.");
        progress.attempts.Single().status=AttemptStatus.Completed;
        Check(!ProgressRules.MergeCheckpoint(progress,Checkpoint(5,5)),"Checkpoint resurrected a completed attempt.");
        var foreign=Checkpoint(1);foreign.attempt.userId="another-account";bool rejected=false;
        try{ProgressRules.MergeCheckpoint(progress,foreign);}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"Checkpoint from another account was accepted.");
        progress.resetGeneration=1;
        Check(!ProgressRules.MergeCheckpoint(progress,first),"Stale reset generation restored a checkpoint.");
    }

    static void CheckFieldOfViewPreferences()
    {
        var legacy=new SettingsProfile{simulatorFov=0,comfortFov=0,masterVolume=.35f};legacy.Validate();
        Check(legacy.simulatorFov==75&&legacy.comfortFov==55&&legacy.schemaVersion==1&&legacy.masterVolume==.35f,"Older profiles need FOV defaults without resetting other settings.");
        legacy.simulatorFov=200;legacy.comfortFov=-10;legacy.Validate();
        Check(legacy.simulatorFov==110&&legacy.comfortFov==40,"FOV bounds must be enforced.");
        legacy.simulatorFov=90;legacy.comfortFov=85;
        var restored=JsonSerializer.Deserialize<SettingsProfile>(JsonSerializer.Serialize(legacy,Json),Json);restored.Validate();
        Check(restored.simulatorFov==90&&restored.comfortFov==85,"FOV choices must survive serialization.");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            foreach(bool simulator in new[]{true,false}){
                var profile=new SettingsProfile();if(simulator)profile.simulatorFov=invalid;else profile.comfortFov=invalid;
                bool rejected=false;try{profile.Validate();}catch(ArgumentException){rejected=true;}
                Check(rejected,"Non-finite FOV must be rejected.");
            }
    }

    static void CheckRefreshPreferences()
    {
        var fresh=new SettingsProfile{platform="Quest"};fresh.Validate();
        Check(fresh.refreshRate==0&&fresh.refreshRevision==1&&fresh.schemaVersion==1,"New Quest profiles must use automatic maximum refresh with the existing save schema.");
        var legacy=JsonSerializer.Deserialize<SettingsProfile>("{\"platform\":\"Quest\",\"refreshRate\":72,\"masterVolume\":0.35}",Json);legacy.Validate();
        Check(legacy.refreshRate==0&&legacy.masterVolume==.35f,"Upgrade must remove the old 72 Hz default without resetting other preferences.");
        legacy.refreshRate=72;legacy.Validate();
        Check(legacy.refreshRate==72,"An explicit 72 Hz choice after upgrading must remain selectable.");
        var custom=new SettingsProfile{platform="Quest",refreshRate=90};custom.Validate();
        Check(custom.refreshRate==90,"Upgrade must preserve a higher custom refresh choice.");
        var restored=JsonSerializer.Deserialize<SettingsProfile>(JsonSerializer.Serialize(legacy,Json),Json);restored.Validate();
        Check(restored.refreshRate==72&&restored.refreshRevision==1,"Saved refresh migration must not run twice.");
        foreach(int invalid in new[]{-1,999}){fresh.refreshRate=invalid;fresh.Validate();Check(fresh.refreshRate==0,"Invalid refresh preference must return to automatic.");}
    }

    static void CheckVRAccessibility()
    {
        var legacy=new SettingsProfile{stickDeadzone=0,textScale=.95f,captionScale=1.3f,movementRevision=1,movement=MoveStyle.Teleport};legacy.Validate();
        Check(legacy.stickDeadzone==.2f&&legacy.textScale==.95f&&legacy.captionScale==1.3f&&legacy.movement==MoveStyle.Teleport,"Adding drift protection must preserve saved reading and movement choices.");
        legacy.stickDeadzone=.4f;
        var restored=JsonSerializer.Deserialize<SettingsProfile>(JsonSerializer.Serialize(legacy,Json),Json);restored.Validate();
        Check(restored.stickDeadzone==.4f&&restored.textScale==.95f,"Accessibility preferences must survive saving.");
        legacy.stickDeadzone=1;legacy.Validate();Check(legacy.stickDeadzone==.45f,"Deadzone must leave usable stick travel.");
        legacy.stickDeadzone=-1;legacy.Validate();Check(legacy.stickDeadzone==.1f,"Deadzone must protect against center drift.");
        foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        {
            var invalid=new SettingsProfile{stickDeadzone=bad};bool rejected=false;
            try{invalid.Validate();}catch(ArgumentException){rejected=true;}
            Check(rejected,"Non-finite deadzone must not reach locomotion.");
        }
        var stick=new VRStickInput();
        (float x,float y) Read(float x,float y,bool tracked=true,XRInteractionSource source=XRInteractionSource.Controller,float zone=.2f)
        {stick.Filter(source,tracked,zone,ref x,ref y);return (x,y);}
        Check(Read(0,1)==(0,0),"A held stick at startup cannot walk.");
        Check(Read(.1f,.1f)==(0,0)&&stick.Ready,"Centered drift should arm the stick without moving.");
        var edge=Read(0,.201f);Check(edge.y>0&&edge.y<.01f,"Crossing the deadzone must start gently, without a speed jump.");
        Check(Math.Abs(Read(0,.6f).y-.5f)<.0001f,"Remaining travel must provide proportional movement.");
        Check(Read(0,-1)==(0,-1),"Full backward travel should retain full configured speed.");
        var diagonal=Read(1,1);Check(Math.Abs(diagonal.x*diagonal.x+diagonal.y*diagonal.y-1)<.0001f,"Diagonal travel must not increase walking speed.");
        Check(Read(0,.3f,zone:.4f)==(0,0),"The learner's larger deadzone must suppress drift.");
        Check(Read(0,1,false)==(0,0)&&!stick.Ready,"Tracking loss must clear readiness.");
        Check(Read(0,1)==(0,0),"Restored tracking cannot replay a held stick.");
        Read(0,0);Check(Read(0,1)==(0,1),"Centering after recovery should restore movement.");
        Check(Read(0,1,source:XRInteractionSource.Simulator)==(0,0),"Switching input sources must require neutral.");
        Read(0,0,source:XRInteractionSource.Simulator);
        Check(Read(0,1,source:XRInteractionSource.Simulator)==(0,1),"The simulator must use the same release gate.");
        stick.Reset();Check(Read(0,1,source:XRInteractionSource.Simulator)==(0,0),"Pause or panel capture must require fresh input.");
        Read(0,0);Check(Read(float.NaN,1)==(0,0)&&!stick.Ready,"Invalid input must stop movement.");
        Check(Read(0,1)==(0,0),"Recovery from invalid input must also require neutral.");
        var snap=new SnapTurnLatch();
        Check(snap.Read(1,0)==0,"Snap turn must wait for an initial release.");snap.Read(0,0);
        Check(snap.Read(.1f,1)==0,"Vertical stick input must not turn the view.");
        Check(snap.Read(1,0)==1,"A deliberate side press must turn once.");
        int repeatedTurns=0;for(int frame=0;frame<120;frame++)repeatedTurns+=snap.Read(1,0);
        Check(repeatedTurns==0,"Holding the stick must not repeat snap turns.");
        Check(snap.Read(-1,0)==0,"Changing sides without centering must not turn again.");
        snap.Read(0,0);Check(snap.Read(-1,0)==-1,"Centering must enable one fresh turn in either direction.");
        snap.Reset();Check(snap.Read(1,0)==0,"Resetting snap turn must discard held input.");
    }

    static void Main(string[] args)
    {
        CheckVRAccessibility();
        InspectionZoomTests.Run(Check);
        CheckFieldOfViewPreferences();
        CheckRefreshPreferences();
        CheckClinicalDecisions();
        CheckBrandStorageCompatibility();
        CheckCheckpointReconciliation();
        var engine = Engine(Objective("rib"), Objective("lung"));
        Check(!engine.Apply(Hit("lung")), "Out-of-order selection credited.");
        var rib = Hit("rib"); Check(engine.Apply(rib), "Valid action rejected.");
        Check(!engine.Apply(rib), "Duplicate event credited.");
        Check(engine.Apply(Hit("lung")), "Second objective rejected.");
        engine.Finish(); Check(engine.Attempt.passed && engine.Attempt.score == 95, "Expected practical pass.");
        Check(!engine.Apply(Hit("other")), "Completed attempt mutated.");

        var critical = Engine(Objective("rib")); critical.Apply(Hit("rib")); critical.Apply(Hit("nerve", ActionKind.ProtectedStructureViolation)); critical.Finish();
        Check(!critical.Attempt.passed && critical.Attempt.criticalFailure, "Critical error did not fail assessment.");
        var assisted = Engine(Objective("rib")); assisted.Apply(Hit("rib")); assisted.MarkAssisted(); assisted.Finish(); Check(!assisted.Attempt.passed, "Hint bypassed mastery.");
        var missing = Engine(Objective("rib"), Objective("lung")); missing.Apply(Hit("rib")); missing.Finish(); Check(!missing.Attempt.passed, "Missing essential objective passed.");

        var precision = Objective("placement", ActionKind.Place); precision.maxDistance = .02f; precision.maxAngle = 10; precision.minContactSeconds = .5f; precision.toolId = "catheter";
        var action = Hit("placement", ActionKind.Place); action.toolId = "catheter"; action.contactSeconds = .6f;
        Check(AssessmentEngine.Matches(precision, action), "Correct precision evidence rejected.");
        action.angle = 11; Check(!AssessmentEngine.Matches(precision, action), "Bad angle credited."); action.angle = 0; action.contactVerified = false;
        Check(!AssessmentEngine.Matches(precision, action), "Unverified contact credited.");
        var invalid = Hit("rib"); invalid.distance = float.NaN; Check(!Engine(Objective("rib")).Apply(invalid), "NaN evidence credited.");

        var progress = new ProgressSnapshot { userId = "account-a", orientationCompleted = true };
        Check(!ProgressRules.IsUnlocked(progress, LearningMode.AnatomyCases, "case", Difficulty.Medium), "Medium opened without Easy.");
        engine.Attempt.mode = LearningMode.AnatomyCases; engine.Attempt.unitId = "case";
        ProgressRules.AddAttempt(progress, engine.Attempt); int xp = progress.rewards.Sum(r => r.xp);
        Check(!ProgressRules.AddAttempt(progress, engine.Attempt) && progress.rewards.Sum(r => r.xp) == xp, "Duplicate reward granted.");
        Check(xp == 20 && progress.rewards.Count == 2, "Rewards were not assigned per mastered objective.");
        AttemptRecord Retry(SessionPhase phase = SessionPhase.Assessment) { var copy = JsonSerializer.Deserialize<AttemptRecord>(JsonSerializer.Serialize(engine.Attempt, Json), Json); copy.id = Guid.NewGuid().ToString("N"); copy.deviceId = "another-device"; copy.phase = phase; return copy; }
        ProgressRules.AddAttempt(progress, Retry());
        Check(progress.rewards.Sum(r => r.xp) == xp, "Retry on another device duplicated objective rewards.");
        ProgressRules.AddAttempt(progress, Retry(SessionPhase.Lesson)); ProgressRules.AddAttempt(progress, Retry(SessionPhase.Practice));
        Check(progress.rewards.Sum(r => r.xp) == xp, "Repeated phases duplicated mastery rewards.");
        var nextDifficulty = Retry(); nextDifficulty.difficulty = Difficulty.Medium; ProgressRules.AddAttempt(progress, nextDifficulty);
        Check(progress.rewards.Count == 4 && progress.rewards.Sum(r => r.xp) == 50, "The next difficulty did not receive separate objective rewards.");
        progress.rewards.Add(new RewardRecord { key = "legacy-phase-reward", xp = 1000 });
        ProgressRules.RebuildRewards(progress); ProgressRules.RebuildRewards(progress);
        Check(progress.rewards.Count == 4 && progress.rewards.Sum(r => r.xp) == 50, "Reward reconciliation retained legacy or duplicate rewards.");
        var practiceOnly = new ProgressSnapshot { userId = "account-a" }; ProgressRules.AddAttempt(practiceOnly, Retry(SessionPhase.Practice));
        Check(practiceOnly.rewards.Count == 0, "Practice counted as assessed mastery.");
        var failedReward = Retry(); failedReward.passed = false; failedReward.difficulty = Difficulty.Hard; ProgressRules.AddAttempt(practiceOnly, failedReward);
        Check(practiceOnly.rewards.Count == 0, "A failed assessment granted mastery rewards.");
        var partialReward = Retry(); partialReward.objectives[1].completed = false; partialReward.objectives.Add(partialReward.objectives[0]); ProgressRules.AddAttempt(practiceOnly, partialReward);
        Check(practiceOnly.rewards.Count == 1 && practiceOnly.rewards[0].xp == 10, "Incomplete or repeated objective results were rewarded.");
        var foreignReward = Retry(); foreignReward.userId = "account-b"; practiceOnly.attempts.Add(foreignReward);
        var staleReward = Retry(); staleReward.resetGeneration = 99; practiceOnly.attempts.Add(staleReward); ProgressRules.RebuildRewards(practiceOnly);
        Check(practiceOnly.rewards.Count == 1, "Reward rebuild accepted foreign or stale records.");
        Check(ProgressRules.IsUnlocked(progress, LearningMode.AnatomyCases, "case", Difficulty.Medium), "Easy did not unlock Medium.");
        Check(!ProgressRules.IsUnlocked(progress, LearningMode.AnatomyCases, "different", Difficulty.Medium), "Unrelated topic unlocked.");
        Check(!ProgressRules.IsUnlocked(progress, LearningMode.IdentifyEnumerate, "case", Difficulty.Medium), "Another mode's mastery unlocked Medium.");
        var legacyProgress=new ProgressSnapshot { userId="account-a", orientationCompleted=true };
        var legacyPass=Retry();legacyPass.contentVersion="2026.09.1";legacyProgress.attempts.Add(legacyPass);
        Check(!ProgressRules.IsUnlocked(legacyProgress,LearningMode.AnatomyCases,"case",Difficulty.Medium),"An old curriculum pass unlocked updated Medium.");
        Check(ProgressRules.IsUnlocked(legacyProgress,LearningMode.AnatomyCases,"case",Difficulty.Medium,"2026.09.1"),"Preserved historical mastery was incorrectly erased.");
        var previousCurriculum=Retry();previousCurriculum.contentVersion="2026.09.2";legacyProgress.attempts.Add(previousCurriculum);
        Check(!ProgressRules.IsUnlocked(legacyProgress,LearningMode.AnatomyCases,"case",Difficulty.Medium),"Previously published v2 mastery unlocked the revised v3 curriculum.");
        Check(ProgressRules.IsUnlocked(legacyProgress,LearningMode.AnatomyCases,"case",Difficulty.Medium,"2026.09.2"),"Previously published v2 history was erased.");
        legacyProgress.attempts.Add(Retry());
        Check(ProgressRules.IsUnlocked(legacyProgress,LearningMode.AnatomyCases,"case",Difficulty.Medium),"A new curriculum Easy pass did not unlock Medium.");
        var resumeDefinition=new ScenarioVariant {id="case-0-0",unitId="case",version=LearningContent.CurrentVersion,mode=LearningMode.AnatomyCases,difficulty=Difficulty.Easy,objectives=new[]{Objective("new-skin")}};
        var oldCheckpoint=new AttemptRecord {userId="account-a",scenarioId=resumeDefinition.id,unitId="case",contentVersion="2026.09.1",mode=LearningMode.AnatomyCases,difficulty=Difficulty.Easy,status=AttemptStatus.Active};
        oldCheckpoint.actions.Add(Hit("old-sternum"));
        Check(!ProgressRules.CanResume(oldCheckpoint,resumeDefinition),"Old checkpoint was relabelled as the new curriculum.");
        bool versionRejected=false;try {new AssessmentEngine(resumeDefinition,oldCheckpoint);}catch(ArgumentException){versionRejected=true;}
        Check(versionRejected&&oldCheckpoint.actions.Single().targetId=="old-sternum","Version mismatch altered old checkpoint evidence.");
        var currentCheckpoint=new AttemptRecord {userId="account-a",scenarioId=resumeDefinition.id,unitId="case",contentVersion=LearningContent.CurrentVersion,mode=LearningMode.AnatomyCases,difficulty=Difficulty.Easy,status=AttemptStatus.Active};
        Check(ProgressRules.CanResume(currentCheckpoint,resumeDefinition),"A compatible current checkpoint could not resume.");
        currentCheckpoint.difficulty=Difficulty.Hard;Check(!ProgressRules.CanResume(currentCheckpoint,resumeDefinition),"Checkpoint difficulty did not match its definition.");
        var oldReward=Retry();oldReward.objectives[0].objectiveId="case-0-0-0";oldReward.objectives[1].completed=false;oldReward.contentVersion="2026.09.1";
        var newReward=Retry();newReward.objectives[0].objectiveId="case-0-0-2-fma7163";newReward.objectives[1].completed=false;
        var objectiveHistory=new ProgressSnapshot {userId="account-a"};ProgressRules.AddAttempt(objectiveHistory,oldReward);ProgressRules.AddAttempt(objectiveHistory,newReward);
        Check(objectiveHistory.rewards.Count==2,"An old index-based objective suppressed a new semantic objective reward.");
        Check((int)Difficulty.Easy == 0 && Enum.GetNames(typeof(Difficulty)).SequenceEqual(new[]{"Easy","Medium","Hard"}), "Easy changed persisted difficulty ordinals.");
        var gated = new ProgressSnapshot { userId = "account-a", orientationCompleted = true };
        Check(ProgressRules.IsUnlocked(gated, LearningMode.AnatomyCases, "case", Difficulty.Easy), "Orientation did not open Easy.");
        Check(!ProgressRules.IsUnlocked(gated, LearningMode.AnatomyCases, "case", (Difficulty)3), "Invalid difficulty unlocked.");
        var forgedMedium = Retry(); forgedMedium.difficulty = Difficulty.Medium; gated.attempts.Add(forgedMedium);
        Check(!ProgressRules.IsUnlocked(gated, LearningMode.AnatomyCases, "case", Difficulty.Hard), "Hard skipped Easy mastery.");
        gated.attempts.Clear();
        foreach (var invalidMastery in new Action<AttemptRecord>[] { a=>a.userId="another-account", a=>a.score=79.99f, a=>a.score=float.NaN, a=>a.score=float.PositiveInfinity, a=>a.assisted=true, a=>a.criticalFailure=true, a=>a.phase=SessionPhase.Practice, a=>a.status=AttemptStatus.Interrupted, a=>a.objectives.Clear(), a=>a.resetGeneration=7 })
        {
            var invalidPass=Retry();invalidMastery(invalidPass);gated.attempts.Clear();gated.attempts.Add(invalidPass);
            Check(!ProgressRules.IsUnlocked(gated,LearningMode.AnatomyCases,"case",Difficulty.Medium),"Invalid mastery evidence unlocked Medium.");
        }
        gated.attempts.Clear();var threshold=Retry();threshold.score=80;gated.attempts.Add(threshold);
        Check(ProgressRules.IsUnlocked(gated,LearningMode.AnatomyCases,"case",Difficulty.Medium),"An 80 percent essential pass did not unlock Medium.");
        gated.attempts.Add(forgedMedium);
        Check(ProgressRules.IsUnlocked(gated,LearningMode.AnatomyCases,"case",Difficulty.Hard),"Easy plus Medium did not unlock Hard.");
        gated.orientationCompleted=false;
        Check(!ProgressRules.IsUnlocked(gated,LearningMode.AnatomyCases,"case",Difficulty.Easy),"Orientation gate was bypassed.");
        var easySequence=new AssessmentEngine(new ScenarioVariant{difficulty=Difficulty.Easy,mode=LearningMode.IdentifyEnumerate,ordered=false,objectives=new[]{Objective("first"),Objective("second")}},new AttemptRecord{phase=SessionPhase.Assessment});
        Check(!easySequence.Apply(Hit("second"))&&easySequence.Current.targetId=="first","Easy accepted a future target while another target was being taught.");
        var layer=Objective("skin",ActionKind.Expose);layer.equipmentState="layer-opened";
        var deeper=Objective("fat",ActionKind.Expose);deeper.equipmentState="layer-opened";
        var dissection=new AssessmentEngine(new ScenarioVariant{difficulty=Difficulty.Hard,mode=LearningMode.GuidedDissection,ordered=false,objectives=new[]{layer,deeper}},new AttemptRecord{phase=SessionPhase.Assessment});
        var premature=Hit("fat",ActionKind.Expose);premature.equipmentState="layer-opened";
        Check(!dissection.Apply(premature),"Dissection skipped an outer layer.");
        Check(!dissection.Apply(Hit("skin",ActionKind.Expose)),"Contact without verified opening/clearance completed exposure.");
        var opened=Hit("skin",ActionKind.Expose);opened.equipmentState="layer-opened";
        Check(dissection.Apply(opened)&&dissection.Current.targetId=="fat","A properly opened layer did not advance to the next tissue.");
        var other = new AttemptRecord { userId = "account-b" }; bool rejected = false; try { ProgressRules.AddAttempt(progress, other); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Account isolation failed.");
        ProgressRules.ApplyReset(progress, 1); Check(progress.attempts.Count == 0 && progress.rewards.Count == 0, "Reset did not clear learning history.");
        rejected = false; try { ProgressRules.AddAttempt(progress, engine.Attempt); } catch (InvalidOperationException) { rejected = true; } Check(rejected, "Old generation resurrected cleared progress.");

        string folder = Path.Combine(Path.GetTempPath(), "thorax-learning-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(folder, "progress.save");
        var store = new AtomicFileStore<ProgressSnapshot>(path, value => JsonSerializer.Serialize(value, Json), value => JsonSerializer.Deserialize<ProgressSnapshot>(value, Json), value => { if (value == null || value.schemaVersion != 1 || value.userId != "account-a") throw new InvalidDataException("Invalid account."); });
        store.Save(progress); progress.orientationCompleted = true; store.Save(progress);
        Check(store.Load(() => null).orientationCompleted, "Save round trip failed.");
        File.WriteAllText(path, "interrupted write"); Check(!store.Load(() => null).orientationCompleted && store.RecoveredBackup, "Backup recovery failed.");
        File.WriteAllText(path + ".bak", "also corrupt"); rejected = false; try { store.Load(() => null); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Corruption silently reset learner history.");
        store.Save(progress);File.WriteAllText(path,"interrupted again");
        Check(store.Load(()=>null).orientationCompleted&&store.RecoveredBackup,"A successful recovery save left an invalid backup behind.");
        Directory.Delete(folder, true);

        var leftChunk=new ObjectiveDefinition{id="skin-left",targetId="skin",action=ActionKind.Expose,equipmentState="chunk-lifted:left",toolId="dissection-scalpel",minPathFraction=.003f};
        var rightChunk=new ObjectiveDefinition{id="skin-right",targetId="skin",action=ActionKind.Expose,equipmentState="chunk-lifted:right",toolId="dissection-scalpel",minPathFraction=.003f,prerequisites=new[]{"skin-left"}};
        var bilateral=Engine(leftChunk,rightChunk);
        var chip=Hit("skin",ActionKind.Expose);chip.toolId="dissection-scalpel";chip.equipmentState="chunk-lifted:left";chip.pathFraction=.00001f;
        Check(!bilateral.Apply(chip),"A tiny chip completed a regional opening.");
        var leftEvidence=Hit("skin",ActionKind.Expose);leftEvidence.toolId="dissection-scalpel";leftEvidence.equipmentState="chunk-lifted:left";leftEvidence.pathFraction=.003f;
        Check(bilateral.Apply(leftEvidence),"Measured left chunk was rejected.");
        var repeatedLeft=Hit("skin",ActionKind.Expose);repeatedLeft.toolId="dissection-scalpel";repeatedLeft.equipmentState="chunk-lifted:left";repeatedLeft.pathFraction=1;
        Check(!bilateral.Apply(repeatedLeft),"Repeating the left side completed the right.");bilateral.Finish();Check(!bilateral.Attempt.passed,"One-sided chest wall earned a pass.");
        var sawStep=new ObjectiveDefinition{id="rib",targetId="rib",action=ActionKind.Expose,toolId="dissection-saw",equipmentState="chunk-lifted:all",minPathFraction=.003f};
        var wrongTool=Hit("rib",ActionKind.Expose);wrongTool.toolId="dissection-scalpel";wrongTool.equipmentState="chunk-lifted:all";wrongTool.pathFraction=1;
        Check(!AssessmentEngine.Matches(sawStep,wrongTool),"Scalpel evidence completed a rib saw step.");

        if (args.Length > 0)
        {
            var catalog = JsonSerializer.Deserialize<LearningCatalog>(File.ReadAllText(args[0]), Json);
            IdentificationFocusTests.Run(catalog,Path.Combine(Path.GetDirectoryName(args[0]),"..","..","anatomy-catalog.json"),Check);
            MuseumCollectionTests.Run(Path.Combine(Path.GetDirectoryName(args[0]),"..","..","anatomy-catalog.json"),Check);
            Check(catalog.units.Length == 12, "Expected six anatomy units and six cases.");
            var variants = catalog.units.SelectMany(u => u.variants).ToArray();
            Check(variants.Length == 72 && variants.Select(v => v.id).Distinct().Count() == 72, "Curriculum coverage is incomplete.");
            foreach (var variant in variants)
            {
                var attempt = new AttemptRecord { userId = "account-a", contentVersion = variant.version, phase = SessionPhase.Assessment };
                var test = new AssessmentEngine(variant, attempt);
                foreach (var objective in variant.objectives)
                {
                    var ev = Hit(objective.targetId, objective.action); ev.toolId = objective.toolId; ev.equipmentState = objective.equipmentState;
                    ev.contactSeconds = objective.minContactSeconds; ev.pathFraction = objective.minPathFraction;
                    Check(test.Apply(ev), "Authored positive path failed: " + variant.id + "/" + objective.id);
                }
                test.Finish(); Check(test.Attempt.passed && test.Attempt.score == 100, "Complete variant failed.");
                var replay = new AssessmentEngine(variant, attempt); Check(replay.Attempt.score == 100, "Checkpoint evidence replay diverged.");
            }
        }
        Console.WriteLine("PASS " + assertions + " assertions: scoring, order, evidence, mastery, idempotence, account isolation, reset and durable save recovery.");
    }

    static void CheckClinicalDecisions()
    {
        var decision=new ObjectiveDefinition{id="safety",targetId="equipment:reason-safety",action=ActionKind.ClinicalDecision,toolId="pause-review",equipmentState="placed",criticalOnWrong=true,choices=new[]{new DecisionOption{id="pause-review",rationale="Stop and reassess."},new DecisionOption{id="continue",rationale="Continuing despite chest discomfort is unsafe."}}};
        InteractionAction Choice(string tool,bool verified=true)=>new InteractionAction{kind=ActionKind.ClinicalDecision,targetId=decision.targetId,toolId=tool,equipmentState="placed",contactVerified=verified};
        var safe=Engine(decision);Check(safe.Apply(Choice("pause-review")),"Physical safe decision rejected.");safe.Finish();Check(safe.Attempt.passed,"Safe decision failed assessment.");
        var unsafeAttempt=Engine(decision);Check(!unsafeAttempt.Apply(Choice("continue")),"Unsafe decision credited.");Check(unsafeAttempt.Attempt.criticalFailure&&unsafeAttempt.Feedback.Contains("unsafe"),"Unsafe branch lacks critical failure and rationale.");
        Check(unsafeAttempt.Apply(Choice("pause-review")),"Correction cannot be recorded.");unsafeAttempt.Finish();Check(!unsafeAttempt.Attempt.passed&&unsafeAttempt.Attempt.score==95,"Correcting an unsafe assessment erased its safety failure.");
        var replay=new AssessmentEngine(new ScenarioVariant{objectives=new[]{decision},ordered=true},unsafeAttempt.Attempt);Check(replay.Attempt.criticalFailure,"Replay erased recorded unsafe choice.");
        var unverified=Engine(decision);unverified.Apply(Choice("continue",false));Check(!unverified.Attempt.criticalFailure,"Unverified contact created critical evidence.");
        var unknown=Engine(decision);unknown.Apply(Choice("forged"));Check(!unknown.Attempt.criticalFailure,"Unknown choice fabricated safety evidence.");
        decision.criticalOnWrong=false;var mechanism=Engine(decision);mechanism.Apply(Choice("continue"));Check(!mechanism.Attempt.criticalFailure,"Ordinary reasoning error became a critical safety failure.");
        Check(mechanism.Apply(Choice("pause-review")),"Reasoning retry failed.");mechanism.Finish();Check(mechanism.Attempt.passed,"Ordinary reasoning retry cannot pass.");
    }

    static void CheckBrandStorageCompatibility()
    {
        var folder=Path.Combine(Path.GetTempPath(),"anatome-brand-tests-"+Guid.NewGuid().ToString("N"));
        var oldPath=Path.Combine(folder,"Thorax Lab","Thorax Lab");
        var newPath=Path.Combine(folder,"Anatome","Anatome");
        var oldRoot=Path.Combine(oldPath,"Learning-v1");
        var current=LearningBrand.DataRoot(newPath,true,"Anatome","Anatome");
        Check(current==oldRoot,"Branding stranded existing Windows data.");
        Check(LearningBrand.DataRoot(oldPath,true,"Thorax Lab","Thorax Lab")==oldRoot,"Legacy product data changed.");
        var testPath=Path.Combine(folder,"Anatome","ThoraxLabAccountVerification");
        Check(LearningBrand.DataRoot(testPath,true,"Anatome","ThoraxLabAccountVerification")==Path.Combine(testPath,"Learning-v1"),"QA product leaked into learner storage.");
        var androidPath=Path.Combine(folder,"Android","data","org.thoraxlab.learning","files");
        Check(LearningBrand.DataRoot(androidPath,false,"Anatome","Anatome")==Path.Combine(androidPath,"Learning-v1"),"Android package storage changed.");
        var profile=new ProgressSnapshot{userId="brand-fixture",orientationCompleted=true};
        var oldStore=new AtomicFileStore<ProgressSnapshot>(Path.Combine(oldRoot,"progress.save"),p=>JsonSerializer.Serialize(p,Json),s=>JsonSerializer.Deserialize<ProgressSnapshot>(s,Json),p=>{if(p?.userId!="brand-fixture")throw new InvalidDataException();});
        oldStore.Save(profile);
        var newStore=new AtomicFileStore<ProgressSnapshot>(Path.Combine(current,"progress.save"),p=>JsonSerializer.Serialize(p,Json),s=>JsonSerializer.Deserialize<ProgressSnapshot>(s,Json),p=>{if(p?.userId!="brand-fixture")throw new InvalidDataException();});
        Check(newStore.Load(()=>null).orientationCompleted,"Branded application could not read an existing atomic save.");
        Check(!Directory.Exists(newPath),"Branding created a parallel empty profile directory.");
        // Only the unique temporary fixture directory created above is removed.
        Directory.Delete(folder,true);
    }
}

