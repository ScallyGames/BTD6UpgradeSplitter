using System;
using System.Linq;
using Assets.Main.Scenes;
using Assets.Scripts.Models.Profile;
using Assets.Scripts.Models.Towers;
using Assets.Scripts.Models.Towers.Upgrades;
using Assets.Scripts.Models.TowerSets;
using Assets.Scripts.Unity;
using Assets.Scripts.Unity.Localization;
using Harmony;
using MelonLoader;
using UnhollowerBaseLib;
using Assets.Scripts.Simulation.Input;
using Assets.Scripts.Unity.UI_New.InGame.RightMenu;
using Assets.Scripts.Unity.UI_New.InGame.StoreMenu;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;
using Assets.Scripts.Unity.UI_New.Upgrade;
using Assets.Scripts.Unity.Bridge;
using Il2CppSystem.Collections.Generic;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;

namespace BTD6UpgradeSplitter
{
    public class Main : BloonsTD6Mod
    {
        public static (int, int, int)[] upgradeGroups = new (int, int, int)[]
        {
            (5, 2, 0),
            (5, 0, 2),
            (2, 5, 0),
            (0, 5, 2),
            (2, 0, 5),
            (0, 2, 5),
        };
        public static List<string> newTowerNames = new List<string>();
        private static bool isSplitInitialized = false;
        private static bool isInventoryInitialized = false;

        public override void OnApplicationStart()
        {
            base.OnApplicationStart();

            MelonLogger.Msg("BTD6UpgradeSplitter mod loaded");
        }

        public static void Initialise()
        {
            MelonLogger.Msg("Initialising");
            var baseTowers = Game.instance.model.towerSet.ToArray();

            int towerIndex = 0;

            List<TowerModel> newTowers = new List<TowerModel>(baseTowers.Length * 6 * 6);
            List<TowerDetailsModel> newShopTowerModels = new List<TowerDetailsModel>(baseTowers.Length * 6);

            foreach (var baseTower in baseTowers)
            {
                foreach (var upgrade in upgradeGroups)
                {
                    string towerTag = baseTower.towerId + $"{upgrade.Item1}{upgrade.Item2}{upgrade.Item3}";
                    string towerDisplayName = LocalizationManager.instance.textTable[baseTower.towerId] + "\r\n" + $"{upgrade.Item1}-{upgrade.Item2}-{upgrade.Item3}";

                    newTowerNames.Add(towerTag);

                    if (!LocalizationManager.instance.textTable.ContainsKey(towerTag))
                    {
                        LocalizationManager.instance.textTable.Add(towerTag, towerDisplayName);
                    }

                    for (int i = 0; i <= upgrade.Item1; i++)
                    {
                        for (int j = 0; j <= upgrade.Item2; j++)
                        {
                            for (int k = 0; k <= upgrade.Item3; k++)
                            {
                                TowerModel towerModel = Game.instance.model.GetTower(baseTower.towerId, i, j, k).Duplicate<TowerModel>();

                                // Upgraded towers need to have same baseId
                                // 0-0-0 has base name
                                // Other upgrades have base name + "-xyz" for the level. The "-" is important!
                                towerModel.name = towerTag + ((i != 0 || j != 0 || k != 0) ? $"-{i}{j}{k}" : "");
                                towerModel.baseId = towerTag;
                                towerModel.dontDisplayUpgrades = false;

                                System.Collections.Generic.List<UpgradePathModel> upgradePaths = new System.Collections.Generic.List<UpgradePathModel>();
                                for (int upgradeIndex = 0; upgradeIndex < towerModel.upgrades.Count; upgradeIndex++)
                                {
                                    var upgradePath = towerModel.upgrades[upgradeIndex];
                                    var upgradeInstance = Game.instance.model.GetUpgrade(upgradePath.upgrade);
                                    var newUpgradeLevel = (
                                        i + (upgradeInstance.path == 0 ? 1 : 0),
                                        j + (upgradeInstance.path == 1 ? 1 : 0),
                                        k + (upgradeInstance.path == 2 ? 1 : 0)
                                    );
                                    if (
                                        newUpgradeLevel.Item1 > upgrade.Item1 ||
                                        newUpgradeLevel.Item2 > upgrade.Item2 ||
                                        newUpgradeLevel.Item3 > upgrade.Item3
                                    )
                                    {
                                        continue;
                                    }

                                    var newPath = new UpgradePathModel(
                                        upgradePath.upgrade,
                                        towerTag + $"-{newUpgradeLevel.Item1}{newUpgradeLevel.Item2}{newUpgradeLevel.Item3}",
                                        upgradePath.numberOfPathsUsed,
                                        upgradePath.tier
                                    );
                                    upgradePaths.Add(newPath);
                                }
                                towerModel.upgrades = new Il2CppReferenceArray<UpgradePathModel>(upgradePaths.ToArray());

                                newTowers.Add(towerModel);
                            }
                        }
                    }

                    newShopTowerModels.Add(new ShopTowerDetailsModel(towerTag, towerIndex, upgrade.Item1, upgrade.Item2, upgrade.Item3, -1, 0, null));

                    towerIndex++;
                }
            }

            Game.instance.model.towers = Game.instance.model.towers.Add(newTowers);
            Game.instance.model.towerSet = Game.instance.model.towerSet.Add<TowerDetailsModel>(newShopTowerModels);

            MelonLogger.Msg("Done");
        }

        public override void OnTitleScreen()
        {
            base.OnTitleScreen();

            if (!isSplitInitialized)
            {
                Initialise();
                isSplitInitialized = true;
            }
        }

        [HarmonyPatch(typeof(ProfileModel), "Validate")]
        public class ProfileModel_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ref ProfileModel __instance)
            {
                foreach (string customTowerName in newTowerNames)
                {
                    var unlockedTowers = __instance.unlockedTowers;
                    if (!unlockedTowers.Contains(customTowerName))
                    {
                        unlockedTowers.Add(customTowerName);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(UpgradeScreen), "UpdateUi")]
        public class UpgradeScreen_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref string towerId, ref string upgradeID)
            {
                towerId = Regex.Replace(towerId, @"\d*", "");
                return true;
            }
        }

        [HarmonyPatch(typeof(Xp), "AddTowerXp")]
        public class Xp_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref string tower, ref float xp)
            {
                tower = Regex.Replace(tower, @"\d*", "");

                return true;
            }
        }


        [HarmonyPatch(typeof(TowerInventory), "Init")]
        public class TowerInit_Patch
        {
            [HarmonyPrefix]
            public static bool Prefix(ref Il2CppSystem.Collections.Generic.List<TowerDetailsModel> allTowersInTheGame)
            {
                var towersToRemove = allTowersInTheGame.ToArray().Where(x => x.name.Contains("Hero"));

                foreach (var towerToRemove in towersToRemove)
                {
                    allTowersInTheGame.Remove(towerToRemove);
                }

                isInventoryInitialized = false;

                return true;
            }
        }

        [HarmonyPatch(typeof(ContentSizeFitter), "SetDirty")]
        public class TowerPurchaseButton_Patch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                if (isInventoryInitialized) return;
                if (ShopMenu.instance == null) return;
                if (ShopMenu.instance.towerButtons == null) return;

                var buttons = ShopMenu.instance.towerButtons.GetComponentsInChildren<TowerPurchaseButton>();
                if (buttons.Count > 0)
                {
                    foreach (var button in buttons)
                    {
                        if(!Regex.IsMatch(button.baseTowerModel.baseId, @"(\d)(\d)(\d)"))
                        {
                            button.transform.parent.gameObject.SetActive(false);
                            continue;
                        }

                        var textElements = button.GetComponentsInChildren<TextMeshProUGUI>();

                        if (textElements.Count != 2) continue;

                        var moneyElement = textElements[1];
                        var newText = GameObject.Instantiate(moneyElement.transform, moneyElement.transform.parent);
                        var match = Regex.Match(button.baseTowerModel.baseId, @"(\d)(\d)(\d)");

                        newText.GetComponent<TextMeshProUGUI>().text = $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}";
                        newText.GetComponent<RectTransform>().position = new Vector3(1711.8f, 890, 0);
                    }

                    isInventoryInitialized = true;
                }
            }
        }
    }
}
