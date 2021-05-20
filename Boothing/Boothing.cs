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
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using MelonLoader;
using UnityEngine.Networking;
using UnhollowerBaseLib;

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

            /* Hook avatar prefab switch function */
            unsafe {
                var switch_to_prefab_avatar = (IntPtr)typeof(VRCAvatarManager).GetField("NativeMethodInfoPtr_Method_Private_Void_GameObject_String_Boolean_Single_Action_Action_0", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var switch_to_booth_cat = new Action<IntPtr, IntPtr, IntPtr, bool, float, IntPtr, IntPtr>(SwitchToBoothCat).Method.MethodHandle.GetFunctionPointer();
                MelonUtils.NativeHookAttach(switch_to_prefab_avatar, switch_to_booth_cat);
                _switchToPrefabAvatarDelegate = Marshal.GetDelegateForFunctionPointer<SwitchToPrefabAvatarDelegate>(*(IntPtr*)(void*)switch_to_prefab_avatar);
            }
        }

        private delegate void SwitchToPrefabAvatarDelegate(IntPtr @this, IntPtr prefab, IntPtr name, bool isSafe, float scale, IntPtr onSuccess, IntPtr onError);
        private static SwitchToPrefabAvatarDelegate _switchToPrefabAvatarDelegate;

        private static void SwitchToBoothCat(IntPtr @this, IntPtr prefab_ptr, IntPtr name_ptr, bool isSafe, float scale, IntPtr onSuccess_ptr, IntPtr onError_ptr) {
            var name = IL2CPP.Il2CppStringToManaged(name_ptr);

            if (s_BoothCat != null && (name == "blocked" || name == "safety" || name == "performance")) {
                /* Replace robot with prefab boothcat */
                prefab_ptr = s_BoothCat.Pointer;
            }

            /* Invoke original function pointer. */
            _switchToPrefabAvatarDelegate(@this, prefab_ptr, name_ptr, isSafe, scale, onSuccess_ptr, onError_ptr);
        }
    }
}
