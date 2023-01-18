using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AmongUs.Data;
using HarmonyLib;
using VentLib.Localization.Attributes;
using VentLib.Logging;
using static VentLib.Localization.Attributes.LocalizedAttribute;

namespace VentLib.Localization;

public static class Localizer
{
    /// <summary>
    /// Get or set the folder where languages for the Localizer exist
    /// </summary>
    public static string LanguageFolder = "./Languages/";
    public static string DefaultLanguage = "English";
    public static string CurrentLanguage
    {
        get => _currentLanguage;
        set {
            bool different = _currentLanguage != value;
            _currentLanguage = value;
            if (!different) return;
            _translations = _loader.Get(_currentLanguage);
            _translationCache.Clear();
        }
    }

    private static Dictionary<string, Language> _translations = null!;
    private static Dictionary<(string, string), string> _translationCache = new();

    private static string _currentLanguage = DefaultLanguage;
    private static LanguageLoader _loader = null!;
    private static string? _root;

    public static string Get(string keyPath, string? assemblyName = null, bool useCache = true)
    {
        assemblyName ??= Assembly.GetCallingAssembly().GetName().Name!;
        assemblyName = _root == assemblyName ? "root" : assemblyName;

        var cacheKey = (keyPath, assemblyName);

        string? translation;
        if (useCache) {
            translation = _translationCache.GetValueOrDefault(cacheKey);
            if (translation != null) return translation;
        }

        if (_translations.TryGetValue(assemblyName, out Language? language))
            return GetValueFromPath(language, keyPath);
        VentLogger.Fatal($"No Translations Exist for \"{assemblyName}\" Attempting to use Root ({_root}) Translations");
        language = _translations["root"];

        translation = GetValueFromPath(language, keyPath);
        if (useCache) _translationCache[cacheKey] = translation;
        return translation;
    }

    public static void Initialize()
    {
        _currentLanguage = DataManager.Settings.Language.CurrentLanguage.ToString();
        _loader = LanguageLoader.Load(LanguageFolder);
        _translations = _loader.Get(CurrentLanguage);
    }

    public static void Load(Assembly assembly)
    {
        string assemblyName = assembly == Vents.rootAssemby ? "root" : assembly.GetName().Name!;
        if (assembly == Vents.rootAssemby)
            _root =  assembly.GetName().Name!;
        VentLogger.Info($"Loading Translations for {assemblyName}");
        if (!_loader.SupportedLanguages.ContainsKey(assemblyName) && assemblyName != "root")
            new DirectoryInfo(LanguageFolder).CreateSubdirectory(assemblyName);

        if (!_translations.TryGetValue(assemblyName, out Language? language))
        {
            VentLogger.Fatal($"No Translations Exist for {assembly.GetName().Name}! Attempting to use Root Translations");
            language = _translations["root"];
        }

        assembly.GetTypes().Do(cls => ReflectionLoader.RegisterClass(cls));
        Inject(language);
    }

    private static void Inject(Language language)
    {
        List<LocalizedAttribute> sortedAttributes = LocalizedAttribute.Attributes.Keys.ToList();
        sortedAttributes.Sort();

        foreach (LocalizedAttribute attribute in sortedAttributes)
        {
            ReflectionObject reflectionObject = LocalizedAttribute.Attributes[attribute];
            if (attribute.Source?.ReflectionType is ReflectionType.Class) continue;
            string value = GetValueFromPath(language, attribute.GetPath());
            reflectionObject.SetValue(value);
        }

        language.Dump();
    }

    private static string GetValueFromPath(Language language, string path, bool createIfNull = true)
    {
        Dictionary<object, object> dictionary = language.Translations;
        bool created = false;

        string[] subPaths = path.Split(".");
        for (int i = 0; i < subPaths.Length - 1; i++)
        {
            string subPath = subPaths[i];
            if (createIfNull && !dictionary.ContainsKey(subPath))
            {
                dictionary[subPath] = new Dictionary<object, object>();
                created = true;
            }
            dictionary = (Dictionary<object, object>)dictionary[subPath];
        }

        string finalPath = subPaths[^1];

        if (createIfNull && !dictionary.ContainsKey(finalPath)) {
            created = true;
            dictionary[finalPath] = "N/A";
        }

        if (created) language.Dump();

        return (string)dictionary[finalPath];
    }
}