using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace SpecialMixingTerminal;

internal static class TerminalIcon
{
    private static Sprite? _sprite;
    private static Texture2D? _texture;

    internal static Sprite Get()
    {
        if (_sprite != null) return _sprite;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SpecialMixingTerminal.TerminalIcon.png")
            ?? throw new InvalidOperationException("Embedded terminal icon is missing.");
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
        {
            name = "Special Mixing Terminal icon",
            hideFlags = HideFlags.DontUnloadUnusedAsset,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        if (!ImageConversion.LoadImage(_texture, new Il2CppStructArray<byte>(bytes.ToArray()), true))
            throw new InvalidOperationException("Could not decode the embedded terminal icon.");
        _sprite = Sprite.Create(_texture, new Rect(0, 0, _texture.width, _texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
        _sprite.name = "Special Mixing Terminal";
        _sprite.hideFlags = HideFlags.DontUnloadUnusedAsset;
        return _sprite;
    }
}
