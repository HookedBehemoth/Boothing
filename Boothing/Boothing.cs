/*
 * Copyright (c) 2021 HookedBehemoth
 *
 * This program is free software; you can redistribute it and/or modify it
 * under the terms and conditions of the GNU General Public License,
 * version 3, as published by the Free Software Foundation.
 *
 * This program is distributed in the hope it will be useful, but WITHOUT
 * ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or
 * FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License for
 * more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using MelonLoader;
using UnityEngine.Networking;

[assembly: MelonInfo(typeof(Boothing.BoothingMod), "Boothing", "1.0.2", "Behemoth")]
[assembly: MelonGame("VRChat", "VRChat")]

namespace Boothing {
    public class BoothingMod : MelonMod {
        private static GameObject s_BoothCat;

        private static IEnumerator LoadBoothCat(string url) {
            /* Start request */
            var www = UnityWebRequestAssetBundle.GetAssetBundle(url);
            www.SendWebRequest();

            /* Note: UnityWebRequestAsyncOperation is incompatible with MelonCoroutines at the moment */
            while (!www.isDone)
                yield return null;

            if (www.isHttpError) {
                MelonLogger.Msg(www.error);
                yield break;
            }

            var bundle = DownloadHandlerAssetBundle.GetContent(www);
            string prefab_path = null;
            foreach (var path in bundle.GetAllAssetNames()) {
                if (path.EndsWith(".prefab")) {
                    prefab_path = path;
                    break;
                }
            }

            if (prefab_path == null) {
                MelonLogger.Msg("Failed to find prefab in assetbundle");
                yield break;
            }

            MelonLogger.Msg($"bundle asset name: {prefab_path}");
            s_BoothCat = bundle.LoadAsset<GameObject>(prefab_path);
            s_BoothCat.hideFlags = HideFlags.DontUnloadUnusedAsset;

            /* Replace avatar prefabs in the VRCAvatarManager... in the player prefab*/
            while (SpawnManager.field_Private_Static_SpawnManager_0 == null)
                yield return new WaitForSeconds(1f);
            var prefab = SpawnManager.field_Private_Static_SpawnManager_0.field_Public_GameObject_0.transform;
            var manager = prefab.Find("ForwardDirection").GetComponent<VRCAvatarManager>();
            foreach (var prop in typeof(VRCAvatarManager).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(prop => prop.Name.StartsWith("field_Public_GameObject_"))) {
                var name = ((GameObject)prop.GetValue(manager)).name;
                if (name == "Avatar_Utility_Base_ERROR" || name == "Avatar_Utility_Base_SAFETY" || name == "Avatar_Utility_Base_BLOCKED_PERFORMANCE")
                    prop.SetValue(manager, s_BoothCat);
            }
        }

        public override void OnApplicationStart() {
            MelonPreferences.CreateCategory("Boothing");
            var entry = MelonPreferences.CreateEntry<string>("Boothing", "AssetBundlePath", null);
            var path = entry.GetValueAsString();

            if (path == null) {
                MelonLogger.Msg("Boothcat AssetBundle path not set. Please set it in the Melonloader preferences under Boothing!AssetbundlePath");
                return;
            }

            /* Load prefab */
            MelonCoroutines.Start(LoadBoothCat(path));
        }
    }
}
