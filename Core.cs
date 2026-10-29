using Il2Cpp;
using Il2CppFishNet.Object;
using Il2CppInterop.Runtime;
using MelonLoader;
using MelonLoader.Utils;
using System.Collections;
using System.Globalization;
using System.Text.Json;
using UnityEngine;
using UnityEngine.AI;

[assembly: MelonInfo(typeof(CustomNPCFramework.Core), "CustomNPCFramework", "1.1.1", "Nullframe Collective")]
[assembly: MelonGame("ZeoWorks", "Slendytubbies 3")]
[assembly: MelonColor(255, 46, 204, 113)]

namespace CustomNPCFramework
{
    public class Core : MelonMod
    {
        public static List<NPCInfo> NPCInfos = new();
        public static Dictionary<string, GameObject[]> LoadedBundles = new(StringComparer.Ordinal);
        public static bool IsInitialized { get; private set; }
        public static bool AssetsReady => IsInitialized && Error == null;
        public static string Error => NPCCatalog.Items.FirstOrDefault(item => item.Error != null)?.Error;

        public override void OnInitializeMelon()
        {
            string directory = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "CustomNPCs");
            Directory.CreateDirectory(directory);

            foreach (string path in Directory.GetDirectories(directory).OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!File.Exists(System.IO.Path.Combine(path, "npc.json"))) continue;

                try
                {
                    NPCInfo info = NPCManifest.Read(path);
                    if (NPCInfos.Any(item => item.Id == info.Id || item.Name == info.Name))
                    {
                        throw new InvalidDataException("Duplicate NPC Id or Name: " + info.Id);
                    }

                    NPCInfos.Add(info);
                }
                catch (Exception exception) when (exception is InvalidDataException or IOException or JsonException or ArgumentException or UnauthorizedAccessException)
                {
                    LoggerInstance.Error("Cannot load NPC " + System.IO.Path.GetFileName(path) + ": " + exception.Message);
                }
            }

            NPCInfos.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
            NPCPayloadReader.Initialize();
            IsInitialized = true;
            LoggerInstance.Msg("Initialized. NPC packs: " + NPCInfos.Count + ".");
        }

        public override void OnUpdate() => NPCSpawn.Update();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName) => NPCSpawn.Clear();

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (!NPCCatalog.Enabled || sceneName is "MainMenu" or "Updater") return;

            foreach (NPCInfo info in NPCCatalog.Items.Where(item => item.IsAvailable))
            {
                try
                {
                    if (!info.UseDummyModel)
                    {
                        EnsureBundle(info);
                    }
                }
                catch (Exception exception)
                {
                    info.Error = "Cannot load NPC " + info.Name + ": " + exception.Message;
                    LoggerInstance.Error(info.Error);
                }
            }
        }

        public static NPCInfo CheckNPCInfos(string npcName) => NPCInfos.FirstOrDefault(info =>
            (info.Name == npcName || info.Id == npcName) && NPCCatalog.Allows(info));

        public static void SpawnCustomNPC(NPCInfo npcInfo, string prefix, Vector3 position, Quaternion rotation)
        {
            if (!NPCCatalog.Allows(npcInfo)) return;

            CPPBDGGKHCB.FONDGACBGAB(prefix + "/" + npcInfo.Name, position, rotation, null);
        }

        internal static void EnsureBundle(NPCInfo info)
        {
            if (LoadedBundles.TryGetValue(info.Name, out var existing) && existing[0]) return;

            var bundle = Il2CppAssetBundleManager.LoadFromFile(System.IO.Path.Combine(info.Path, "bundle"));
            if (bundle == null) throw new InvalidDataException("The asset bundle could not be loaded.");

            try
            {
                GameObject model = bundle.Load<GameObject>(info.Name);
                bool death = info.Bot != null || info.BossBot != null;
                GameObject dead = death ? bundle.Load<GameObject>(info.Name + (info.Bot?.UseRagdoll == true ? "(Ragdoll)" : "(Dead)")) : null;

                if (!model || death && !dead)
                {
                    throw new InvalidDataException("Missing NPC model or death prefab for " + info.Name + ".");
                }

                LoadedBundles[info.Name] = death ? [model, dead] : [model];
            }
            finally
            {
                bundle.Unload(false);
            }
        }

        internal static void ApplyStatistics(NPCInfo info, GameObject dummy)
        {
            if (info.Bot is { } data)
            {
                Bot bot = dummy.GetComponent<Bot>();
                bot.NNIBHOMNJHM = data.Health;
                bot.CJKBHAHOLMJ = data.Damage;
                bot.DHHNMAIFIFB = data.Speed;
                bot.IPOFBAGEDJK = data.AttackRange;
                bot.LIBBFHCIHID = data.AttackSpeed;
            }
            else if (info.BossBot is { } bossData)
            {
                BossBot boss = dummy.GetComponent<BossBot>();
                boss.NNIBHOMNJHM = bossData.Health;
                boss.CJKBHAHOLMJ = bossData.Damage;
                boss.DHHNMAIFIFB = bossData.Speed;
                boss.GKFPAHIPDOM = bossData.RunSpeed;
                boss.IPOFBAGEDJK = bossData.AttackRange;
                boss.LIBBFHCIHID = bossData.AttackSpeed;
            }
            else if (info.CustardBot is { } custardData)
            {
                CustardBot custard = dummy.GetComponent<CustardBot>();
                custard.CJKBHAHOLMJ = custardData.Damage;
                custard.DHHNMAIFIFB = custardData.Speed;
                custard.GKFPAHIPDOM = custardData.RunSpeed;
            }
            else if (info.PlayerMonster is { } playerData)
            {
                PlayerMonster player = dummy.GetComponent<PlayerMonster>();
                player.OMFJFIPPGPE = Convert.ToInt32(playerData.Damage);
                if (player.GFHPOIPBLGE == null || player.GFHPOIPBLGE.GKJOHCHABPI == null) return;

                player.GFHPOIPBLGE.GKJOHCHABPI.HKCDMBALAAK.WalkSpeed = playerData.Speed;
                player.GFHPOIPBLGE.GKJOHCHABPI.HKCDMBALAAK.RunSpeed = playerData.RunSpeed;
                player.GFHPOIPBLGE.GKJOHCHABPI.AALHECCKHFD.baseHeight = playerData.JumpSpeed;
            }
        }

        private static void StyleModel(NPCInfo info, GameObject model)
        {
            model.transform.localScale *= info.ModelScale;

            if (info.ModelColor == null) return;

            if (!ColorUtility.TryParseHtmlString(info.ModelColor, out Color color))
            {
                throw new InvalidDataException("Invalid ModelColor: " + info.ModelColor);
            }

            foreach (var component in model.GetComponentsInChildren(Il2CppType.Of<Renderer>(), true))
            {
                foreach (Material material in component.Cast<Renderer>().materials)
                {
                    if (material.HasProperty("_Color")) material.color = color;
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                }
            }
        }

        public static void SetupCustomNPC(string npcName, GameObject dummy, NPCType type)
        {
            NPCInfo npcInfo = CheckNPCInfos(npcName);
            if (npcInfo == null) return;

            NPCSpawn.State state = NPCSpawn.Get(dummy) ?? NPCSpawn.Attach(dummy, npcInfo, 0);
            if (state == null || state.Applied) return;

            MelonDebug.Msg("Loading custom NPC: " + npcName);

            Transform original = dummy.transform.Find("Model");
            if (original == null) throw new InvalidOperationException("The dummy prefab has no Model child.");

            if (npcInfo.UseDummyModel)
            {
                ApplyStatistics(npcInfo, dummy);
                StyleModel(npcInfo, original.gameObject);
                state.Applied = true;
                return;
            }

            EnsureBundle(npcInfo);
            original.gameObject.SetActive(false);

            GameObject model = ReplaceModel(npcInfo, dummy);
            MecanimControl mecanimControl = model.AddComponent<MecanimControl>();

            switch (type)
            {
                case NPCType.BossBot:
                    SetupBossBot(npcInfo, dummy, model, mecanimControl);
                    break;
                case NPCType.Bot:
                    SetupBot(npcInfo, dummy, model, mecanimControl);
                    break;
                case NPCType.CustardBot:
                    SetupCustardBot(npcInfo, dummy, model, mecanimControl);
                    break;
                case NPCType.PlayerMonster:
                    SetupPlayerMonster(npcInfo, dummy, mecanimControl);
                    break;
            }

            RegisterAnimations(model, mecanimControl);

            NavMeshAgent agent = dummy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                NetworkObject networkObject = dummy.GetComponent<NetworkObject>();
                agent.enabled = networkObject != null && networkObject.IsServer;
            }

            state.Applied = true;
            dummy.name = npcInfo.Name;
        }

        private static GameObject ReplaceModel(NPCInfo npcInfo, GameObject dummy)
        {
            GameObject model = GameObject.Instantiate(LoadedBundles[npcInfo.Name][0]);
            model.transform.SetParent(dummy.transform);
            dummy.transform.localScale = Vector3.Scale(dummy.transform.localScale, model.transform.localScale);
            model.transform.localScale = new Vector3(1f, 1f, 1f);
            model.transform.localPosition = new Vector3(0f, 0f, 0f);
            model.transform.localEulerAngles = Vector3.zero;
            StyleModel(npcInfo, model);
            SetLayerAllChildren(model.transform, 8);
            return model;
        }

        private static void SetupBossBot(NPCInfo npcInfo, GameObject dummy, GameObject model, MecanimControl mecanimControl)
        {
            BossBotData data = npcInfo.BossBot;
            BossBot bossBot = dummy.GetComponent<BossBot>();
            CopyAgent(dummy, model);

            List<(Collider Collider, float Damage)> hitboxes = CustomHitboxes(model);
            if (hitboxes.Count > 0)
            {
                foreach (HitboxBoss hitbox in dummy.GetComponentsInChildren<HitboxBoss>())
                {
                    GameObject.Destroy(hitbox.gameObject);
                }
            }

            foreach (var (collider, damage) in hitboxes)
            {
                HitboxBoss hitbox = collider.gameObject.AddComponent<HitboxBoss>();
                hitbox.DHGDMKPLBJH = damage;
                hitbox.KOKDGLGNJEN = bossBot;
            }

            bossBot.NNIBHOMNJHM = data.Health;
            bossBot.DHHNMAIFIFB = data.Speed;
            bossBot.CJKBHAHOLMJ = data.Damage;
            bossBot.GKFPAHIPDOM = data.RunSpeed;
            bossBot.IPOFBAGEDJK = data.AttackRange;
            bossBot.LIBBFHCIHID = data.AttackSpeed;
            bossBot.ELPJDJFCCNJ = mecanimControl;
            bossBot.OGJJNCKKILE = LoadedBundles[npcInfo.Name][1].transform;

            if (bossBot.OGJJNCKKILE.GetComponent<DeadBot>() == null)
            {
                bossBot.OGJJNCKKILE.gameObject.AddComponent<DeadBot>().CPBLCBMLFAJ = 15f;
            }
        }

        private static void SetupBot(NPCInfo npcInfo, GameObject dummy, GameObject model, MecanimControl mecanimControl)
        {
            BotData data = npcInfo.Bot;
            Bot bot = dummy.GetComponent<Bot>();
            CopyAgent(dummy, model);

            List<(Collider Collider, float Damage)> hitboxes = CustomHitboxes(model);
            if (hitboxes.Count > 0)
            {
                foreach (HitBoxBot hitbox in dummy.GetComponentsInChildren<HitBoxBot>())
                {
                    GameObject.Destroy(hitbox.gameObject);
                }
            }

            foreach (var (collider, damage) in hitboxes)
            {
                HitBoxBot hitbox = collider.gameObject.AddComponent<HitBoxBot>();
                hitbox.DHGDMKPLBJH = damage;
                hitbox.KOKDGLGNJEN = bot;
            }

            GameObject deadPrefab = LoadedBundles[npcInfo.Name][1];
            bot.OGJJNCKKILE = deadPrefab.transform;

            if (!data.UseRagdoll && bot.OGJJNCKKILE.gameObject.GetComponent<DeadBot>() == null)
            {
                bot.OGJJNCKKILE.gameObject.AddComponent<DeadBot>().CPBLCBMLFAJ = 15f;
            }
            else if (data.UseRagdoll && bot.OGJJNCKKILE.gameObject.GetComponent<WaitForDestroy>() == null)
            {
                bot.OGJJNCKKILE.gameObject.AddComponent<WaitForDestroy>().PHADIEJAKDA = 15f;
            }

            if (data.UseRage)
            {
                SetupRage(data, dummy, bot);
            }

            bot.CJKBHAHOLMJ = data.Damage;
            bot.DHHNMAIFIFB = data.Speed;
            bot.NNIBHOMNJHM = data.Health;
            bot.IPOFBAGEDJK = data.AttackRange;
            bot.LIBBFHCIHID = data.AttackSpeed;
            bot.ELPJDJFCCNJ = mecanimControl;
        }

        private static void SetupRage(BotData data, GameObject dummy, Bot bot)
        {
            BotMorpher botMorpher = dummy.GetComponent<BotMorpher>();
            if (botMorpher == null)
            {
                botMorpher = dummy.AddComponent<BotMorpher>();
                botMorpher.INHFFFAKNNG = bot;
            }

            botMorpher.DHHNMAIFIFB = data.RageRunSpeed;
            botMorpher.HBPNKDOBIFO = data.RageRequiredHp;
            botMorpher.LIBBFHCIHID = data.RageAttackTime;
            botMorpher.AMEBGBAKKAL = "RageRun";
            botMorpher.HNJEODLDKHF = "RageAttack";
        }

        private static void SetupCustardBot(NPCInfo npcInfo, GameObject dummy, GameObject model, MecanimControl mecanimControl)
        {
            CustardBotData data = npcInfo.CustardBot;
            CustardBot custardBot = dummy.GetComponent<CustardBot>();
            CopyAgent(dummy, model);

            custardBot.CJKBHAHOLMJ = data.Damage;
            custardBot.DHHNMAIFIFB = data.Speed;
            custardBot.GKFPAHIPDOM = data.RunSpeed;
            custardBot.ELPJDJFCCNJ = mecanimControl;

            custardBot.KAMFBFKCKLI = model.transform.GetComponentInChildren<AudioSource>();
            if (custardBot.KAMFBFKCKLI != null)
            {
                custardBot.FIKNJKJMAHO = custardBot.KAMFBFKCKLI.clip;
            }
        }

        private static void SetupPlayerMonster(NPCInfo npcInfo, GameObject dummy, MecanimControl mecanimControl)
        {
            PlayerMonster playerMonster = dummy.GetComponent<PlayerMonster>();
            playerMonster.OMFJFIPPGPE = Convert.ToInt32(npcInfo.PlayerMonster.Damage);
            ApplyStatistics(npcInfo, dummy);
            playerMonster.ELPJDJFCCNJ = mecanimControl;
        }

        private static void CopyAgent(GameObject dummy, GameObject model)
        {
            NavMeshAgent modelAgent = model.GetComponent<NavMeshAgent>();
            if (modelAgent == null) return;

            NavMeshAgent dummyAgent = dummy.GetComponent<NavMeshAgent>();
            if (dummyAgent == null) throw new InvalidOperationException("The dummy has no NavMeshAgent.");

            dummyAgent.radius = modelAgent.radius;
            dummyAgent.baseOffset = modelAgent.baseOffset;
            modelAgent.enabled = false;
        }

        private static List<(Collider Collider, float Damage)> CustomHitboxes(GameObject model)
        {
            List<(Collider, float)> hitboxes = new();
            foreach (Collider collider in model.transform.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger || !collider.name.Contains("Hitbox")) continue;

                if (!float.TryParse(collider.name.Split(' ').Last(), NumberStyles.Float, CultureInfo.InvariantCulture, out float multiplier) ||
                    !float.IsFinite(multiplier) || multiplier <= 0)
                {
                    throw new InvalidDataException("Invalid hitbox multiplier: " + collider.name);
                }

                hitboxes.Add((collider, 30 * multiplier));
            }

            return hitboxes;
        }

        private static void RegisterAnimations(GameObject model, MecanimControl mecanimControl)
        {
            Animator animator = model.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips.Where(clip => clip.name == "Idle").Concat(clips.Where(clip => clip.name != "Idle")))
            {
                mecanimControl.CHEKKJFKBLI(clip, clip.name, 1f, clip.isLooping ? WrapMode.Loop : WrapMode.Clamp);
            }
        }

        public static void SetLayerAllChildren(Transform root, int layer)
        {
            var children = root.GetComponentsInChildren(Il2CppType.Of<Transform>(), true).Select(component => component.Cast<Transform>());
            foreach (var child in children)
            {
                child.gameObject.layer = layer;
            }
        }

        public static IEnumerator AddNPCInfos(Volume sandboxConsole)
        {
            if (!NPCCatalog.Enabled) yield break;

            yield return MelonCoroutines.Start(LoadNPCIcons());

            if (!sandboxConsole || !NPCCatalog.Enabled) yield break;

            string[] folders = ["NPCTeamA/", "NPCTeamB/", "PlayerTeamA/", "PlayerTeamB/"];
            foreach (NPCInfo npc in NPCCatalog.Items.Where(NPCCatalog.Allows))
            {
                int first = npc.PlayerMonster != null ? 2 : 0;
                for (int category = first; category < first + 2 && category < sandboxConsole.PDKPIOHFCCK.Count; category++)
                {
                    var target = sandboxConsole.PDKPIOHFCCK[category];
                    if (target == null || target.options == null) continue;

                    string resource = folders[category] + npc.Name;
                    if (HasOption(target.options, resource)) continue;

                    target.options.Add(new Volume.option { image = npc.IconSprite, optionName = npc.Name, resourcePath = resource });
                }
            }

            sandboxConsole.ResetOptions();
        }

        private static bool HasOption(Il2CppSystem.Collections.Generic.List<Volume.option> options, string resource)
        {
            foreach (Volume.option option in options)
            {
                if (option.resourcePath == resource) return true;
            }

            return false;
        }

        public static IEnumerator LoadNPCIcons()
        {
            foreach (NPCInfo info in NPCCatalog.Items.Where(NPCCatalog.Allows))
            {
                string path = System.IO.Path.Combine(info.Path, "icon.png");
                if (info.IconSprite || !File.Exists(path)) continue;

                WWW request = new(new Uri(path).AbsoluteUri);
                yield return request;

                try
                {
                    if (!string.IsNullOrEmpty(request.error)) continue;

                    Texture2D texture = request.texture;
                    if (!texture) continue;

                    info.IconSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                }
                finally
                {
                    request.Dispose();
                }
            }
        }
    }
}
