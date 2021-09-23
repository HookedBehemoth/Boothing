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

using System;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Networking;
using MelonLoader;

[assembly: MelonInfo(typeof(Boothing.BoothingMod), "Boothing", "1.1.0", "Behemoth")]
[assembly: MelonGame("VRChat", "VRChat")]

namespace Boothing {
    public class BoothingMod : MelonMod {
        private static GameObject s_BoothCat;
        private static string[] TargetFieldValueNames = {
            "Avatar_Utility_Base_ERROR",
            "Avatar_Utility_Base_SAFETY",
            "Avatar_Utility_Base_BLOCKED_PERFORMANCE"
        };
        private static List<PropertyInfo> TargetFields = new(3);

        private static IEnumerator LoadBoothCat(string url) {
            /* Start request */
            var www = UnityWebRequestAssetBundle.GetAssetBundle(url);
            www.SendWebRequest();

            /* Note: UnityWebRequestAsyncOperation is incompatible with MelonCoroutines at the moment */
            while (!www.isDone)
                yield return null;

            if (www.isHttpError) {
                MelonLogger.Error($"Failed to load bootcat assetbundle at path \"{url}\"");
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
                MelonLogger.Error("Failed to find prefab in assetbundle");
                yield break;
            }

            MelonLogger.Msg($"bundle asset name: {prefab_path}");
            var temp = bundle.LoadAsset<GameObject>(prefab_path);
            if (s_BoothCat != null) {
                s_BoothCat.hideFlags = HideFlags.None;
                GameObject.Destroy(s_BoothCat);
            }
            s_BoothCat = temp;
            s_BoothCat.hideFlags = HideFlags.DontUnloadUnusedAsset;

            /* Replace avatar prefabs in the VRCAvatarManager... in the player prefab*/
            while (SpawnManager.field_Private_Static_SpawnManager_0 == null)
                yield return new WaitForSeconds(1f);
            var prefab = SpawnManager.field_Private_Static_SpawnManager_0.field_Public_GameObject_0.transform;
            var manager = prefab.Find("ForwardDirection").GetComponent<VRCAvatarManager>();

            /* Populate list if needed */
            if (TargetFields.Count == 0) {
                foreach (var prop in typeof(VRCAvatarManager).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(prop => prop.Name.StartsWith("field_Public_GameObject_"))) {
                    var name = ((GameObject)prop.GetValue(manager))?.name;
                    if (name != null && TargetFieldValueNames.Contains(name)) {
                        TargetFields.Add(prop);
                        prop.SetValue(manager, s_BoothCat);
                    }
                }
            } else {
                /* Replace the values */
                foreach (var prop in TargetFields)
                    prop.SetValue(manager, s_BoothCat);
            }

            /* Maybe apply to loaded players */
            foreach (var player in VRC.PlayerManager.field_Private_Static_PlayerManager_0?.field_Private_List_1_Player_0)
                foreach (var prop in TargetFields)
                    prop.SetValue(player._vrcplayer.prop_VRCAvatarManager_0, s_BoothCat);

            MelonLogger.Msg(ConsoleColor.Green, "Booth cat loaded");
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct UniTaskStructBool {
            [FieldOffset(0)] public IntPtr source;
            [FieldOffset(8)] public bool result;
            [FieldOffset(10)] public short token;
        }

        private static unsafe IntPtr SwitchToFallbackAvatarStub(IntPtr taskStorage, IntPtr managerPtr, IntPtr apiAvatarPtr, float scale) {
            var task = (UniTaskStructBool*)taskStorage;
            var manager = new VRCAvatarManager(managerPtr);
            var result = manager.Method_Private_UniTask_Single_0(scale);
            task->source = result.source.Pointer;
            task->result = true;
            task->token = result.token;
            return taskStorage;
        }

        private static unsafe IntPtr SwitchToPerformanceAvatarStub(IntPtr taskStorage, IntPtr managerPtr, float scale) {
            var task = (UniTaskStructBool*)taskStorage;
            var manager = new VRCAvatarManager(managerPtr);
            var result = manager.Method_Private_UniTask_Single_0(scale);
            task->source = result.source.Pointer;
            task->result = true;
            task->token = result.token;
            return taskStorage;
        }

        public override void OnApplicationStart() {
            /* fallback -> safety */
            unsafe {
                var originalMethodPointer = *(IntPtr*) (IntPtr)typeof(VRCAvatarManager).GetField("NativeMethodInfoPtr_Method_Private_UniTask_1_Boolean_ApiAvatar_Single_2", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var detourPointer = typeof(BoothingMod).GetMethod(nameof(SwitchToFallbackAvatarStub), BindingFlags.Static | BindingFlags.NonPublic).MethodHandle.GetFunctionPointer();
                MelonUtils.NativeHookAttach((IntPtr)(&originalMethodPointer), detourPointer);
            }

            /* performance -> safety */
            unsafe {
                var originalMethodPointer = *(IntPtr*) (IntPtr)typeof(VRCAvatarManager).GetField("NativeMethodInfoPtr_Method_Private_UniTask_Single_1", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                var detourPointer = typeof(BoothingMod).GetMethod(nameof(SwitchToPerformanceAvatarStub), BindingFlags.Static | BindingFlags.NonPublic).MethodHandle.GetFunctionPointer();
                MelonUtils.NativeHookAttach((IntPtr)(&originalMethodPointer), detourPointer);
            }

            var category = MelonPreferences.CreateCategory("Boothing");
            var entry = category.CreateEntry<string>("AssetBundlePath", null);
            entry.OnValueChanged += (old, value) => MelonCoroutines.Start(LoadBoothCat(value));
            var path = entry.Value;

            if (string.IsNullOrEmpty(path)) {
                MelonLogger.Warning("Boothcat AssetBundle path not set. Please set it in the Melonloader preferences under Boothing!AssetbundlePath");
                return;
            }

            /* Load prefab */
            MelonCoroutines.Start(LoadBoothCat(path));
        }
    }
}
