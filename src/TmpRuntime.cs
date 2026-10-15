using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace UnityVietnameseFontSync
{
    // This adapter deliberately has no compile-time dependency on a game's TMP assembly.
    internal sealed class TmpRuntime
    {
        public Type FontType { get; }
        public Type TextType { get; }
        private readonly MethodInfo _create, _addCharacters, _readDefinition, _refresh, _dirty;
        private readonly PropertyInfo _characters, _fallbacks, _mode, _textures, _textFont;
        private readonly MemberInfo _material;

        /// <summary>
        /// Validates the reflected TMP API and enum values before allowing font creation or mutation.
        /// </summary>
        public TmpRuntime(Type fontType, Type textType, Type unityFontType)
        {
            FontType = fontType;
            TextType = textType;
            _characters = RequireProperty(fontType, "characterTable");
            _fallbacks = RequireProperty(fontType, "fallbackFontAssetTable", true);
            _mode = RequireProperty(fontType, "atlasPopulationMode", true);
            _textures = RequireProperty(fontType, "atlasTextures");
            _textFont = RequireProperty(textType, "font");
            _material = (MemberInfo)fontType.GetProperty("material") ?? fontType.GetField("material");
            if (_material == null) throw new NotSupportedException("TMP font material is unavailable.");
            _create = fontType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(method =>
            {
                var p = method.GetParameters();
                return method.Name == "CreateFontAsset" && p.Length >= 8 &&
                    p[0].ParameterType == unityFontType && p[1].ParameterType == typeof(int) &&
                    p[2].ParameterType == typeof(int) && p[3].ParameterType.IsEnum &&
                    p[4].ParameterType == typeof(int) && p[5].ParameterType == typeof(int) &&
                    p[6].ParameterType == _mode.PropertyType && p[7].ParameterType == typeof(bool) &&
                    p.Skip(8).All(parameter => parameter.HasDefaultValue);
            });
            if (_create == null) throw new NotSupportedException("TMP lacks a compatible runtime Font-to-SDF creation overload.");
            _addCharacters = RequireMethod(fontType, "TryAddCharacters", typeof(uint[]), typeof(uint[]).MakeByRefType(), typeof(bool));
            _readDefinition = RequireMethod(fontType, "ReadFontAssetDefinition");
            _refresh = RequireMethod(textType, "ForceMeshUpdate", typeof(bool), typeof(bool));
            _dirty = RequireMethod(textType, "SetAllDirty");
            // Validate enum names at startup, before any game assets can be modified.
            Enum.Parse(_mode.PropertyType, "Static");
            Enum.Parse(_mode.PropertyType, "Dynamic");
            Enum.Parse(_create.GetParameters()[3].ParameterType, "SDFAA");
        }

        /// <summary>
        /// Finds TMP font and text types in a loaded assembly, or throws when no supported API is available.
        /// </summary>
        public static TmpRuntime Discover(Type unityFontType)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type font = assembly.GetType("TMPro.TMP_FontAsset", false);
                Type text = assembly.GetType("TMPro.TMP_Text", false);
                if (font != null && text != null) return new TmpRuntime(font, text, unityFontType);
            }
            throw new NotSupportedException("No loaded TMP_FontAsset/TMP_Text API found. Legacy UI and IL2CPP are not supported.");
        }

        /// <summary>
        /// Creates a dynamic SDF font with a square atlas, using declared defaults for optional trailing parameters.
        /// </summary>
        public object Create(object source, int size, int padding, int atlas, bool multiAtlas)
        {
            var parameters = _create.GetParameters();
            var arguments = new object[parameters.Length];
            object[] required = { source, size, padding, Enum.Parse(parameters[3].ParameterType, "SDFAA"),
                atlas, atlas, Enum.Parse(_mode.PropertyType, "Dynamic"), multiAtlas };
            Array.Copy(required, arguments, required.Length);
            for (int i = required.Length; i < arguments.Length; i++) arguments[i] = parameters[i].DefaultValue;
            return _create.Invoke(null, arguments);
        }

        /// <summary>
        /// Attempts to populate glyphs and returns the TMP completion result and missing character codes.
        /// </summary>
        public bool AddCharacters(object font, uint[] codes, out uint[] missing)
        {
            object[] arguments = { codes, null, false };
            bool complete = (bool)_addCharacters.Invoke(font, arguments);
            missing = (uint[])arguments[1];
            return complete;
        }

        /// <summary>
        /// Returns the mutable character table of the reflected font asset.
        /// </summary>
        public IList Characters(object font) => (IList)_characters.GetValue(font, null);
        /// <summary>
        /// Returns the font asset fallback list, which may be null.
        /// </summary>
        public IList Fallbacks(object font) => (IList)_fallbacks.GetValue(font, null);
        /// <summary>
        /// Assigns the font asset fallback list, including a null list when restoring original state.
        /// </summary>
        public void SetFallbacks(object font, IList value) => _fallbacks.SetValue(font, value, null);
        /// <summary>
        /// Creates an empty list of the type required by the TMP fallback property.
        /// </summary>
        public IList NewFallbacks() => (IList)Activator.CreateInstance(_fallbacks.PropertyType);
        /// <summary>
        /// Returns the font asset atlas population mode.
        /// </summary>
        public object Mode(object font) => _mode.GetValue(font, null);
        /// <summary>
        /// Assigns the font asset atlas population mode.
        /// </summary>
        public void SetMode(object font, object mode) => _mode.SetValue(font, mode, null);
        /// <summary>
        /// Sets the font asset atlas population mode to Static.
        /// </summary>
        public void Freeze(object font) => SetMode(font, Enum.Parse(_mode.PropertyType, "Static"));
        /// <summary>
        /// Returns the atlas textures currently associated with the font asset.
        /// </summary>
        public IEnumerable Textures(object font) => (IEnumerable)_textures.GetValue(font, null);
        /// <summary>
        /// Reads the font material through the available reflected property or field.
        /// </summary>
        public object Material(object font) => _material is PropertyInfo property
            ? property.GetValue(font, null) : ((FieldInfo)_material).GetValue(font);
        /// <summary>
        /// Returns the text component primary font asset.
        /// </summary>
        public object TextFont(object text) => _textFont.GetValue(text, null);
        /// <summary>
        /// Rebuilds the font asset definition after its tables change.
        /// </summary>
        public void ReadDefinition(object font) => _readDefinition.Invoke(font, null);
        /// <summary>
        /// Marks text dirty and forces a mesh update with inactive objects and reparsing enabled.
        /// </summary>
        public void Refresh(object text)
        {
            _dirty.Invoke(text, null);
            _refresh.Invoke(text, new object[] { true, true });
        }

        /// <summary>
        /// Returns a readable property, optionally requiring write access, or throws for an unsupported API.
        /// </summary>
        private static PropertyInfo RequireProperty(Type type, string name, bool writable = false)
        {
            PropertyInfo property = type.GetProperty(name);
            if (property == null || !property.CanRead || (writable && !property.CanWrite))
                throw new NotSupportedException(type.FullName + "." + name + " is unavailable.");
            return property;
        }

        /// <summary>
        /// Returns the method with the required parameter types, or throws when the overload is absent.
        /// </summary>
        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
        {
            return type.GetMethod(name, parameters) ?? throw new NotSupportedException(type.FullName + "." + name + " has no compatible overload.");
        }
    }
}
