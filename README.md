# laughing-octo-parakeet

VRChat modification intended to be loaded with Melonloader 0.3.0+.

Allows you to replace the default robot avatars with an avatar of your choice.

Simply add these lines to your MelonPreferences.cfg with a path to a vrca file.
__data avatar files and remote links should work too.

```ini
[Boothing]
AssetBundlePath = "D:\\Path\\To\\boothcat.vrca"
```

# Note
* You can "Build & Test" in the SDK and grab the vrca file from your cache directory (e.g. `C:\\Users\\(you)\\AppData\\LocalLow\\VRChat\\VRChat\\Avatars`).
* Optimize the avatar you want to use (you'll see it a lot).
* Don't name any mesh renderer "Body".
  * On performance or size blocked avatars, VRChat alters the "Tint" shader attribute on the first Material of the Renderer with that name.
