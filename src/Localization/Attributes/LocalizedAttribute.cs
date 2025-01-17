using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;

namespace VentLib.Localization.Attributes;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class)]
public class LocalizedAttribute : Attribute
{
    internal static Dictionary<Type, string> ClassQualifiers = new();

    public string Qualifier;
    public bool IgnoreNesting;

    internal Type DeclaringType = null!;
    
    public LocalizedAttribute(string qualifier, bool ignoreNesting = false)
    {
        Qualifier = qualifier;
        IgnoreNesting = ignoreNesting;
    }

    public void Register(Localizer localizer, Type type)
    {
        Register(localizer, type, new[] { this });
    }

    public void Register(Localizer localizer, Type type, LocalizedAttribute[] parentAttributes)
    {
        ClassQualifiers[type] = Qualifier;
        DeclaringType = type;
        
        type.GetNestedTypes(AccessFlags.AllAccessFlags)
            .Where(t => t.GetCustomAttribute<LocalizedAttribute>() != null)
            .ForEach(t =>
            {
                LocalizedAttribute attribute = t.GetCustomAttribute<LocalizedAttribute>()!;
                attribute.Register(localizer, t, parentAttributes.AddItem(attribute).ToArray());
            });
        
        type.GetFields(AccessFlags.StaticAccessFlags)
            .Where(field => field.GetCustomAttribute<LocalizedAttribute>() != null)
            .ForEach(field => field.GetCustomAttribute<LocalizedAttribute>()!.Inject(localizer, parentAttributes, field));
    }

    public void Inject(Localizer localizer, LocalizedAttribute[] parentAttributes, FieldInfo containingField)
    {
        string qualifier = "";
        foreach (LocalizedAttribute parentAttribute in parentAttributes)
        {
            string classQualifier = ClassQualifiers.GetValueOrDefault(parentAttribute.DeclaringType, "");
            
            if (qualifier == "" || parentAttribute.IgnoreNesting)
                qualifier = classQualifier;
            else if (classQualifier != "")
                qualifier = qualifier + "." + classQualifier;
            
        }

        LocalizedAttribute fieldAttribute = containingField.GetCustomAttribute<LocalizedAttribute>()!;
        if (fieldAttribute.IgnoreNesting) qualifier = fieldAttribute.Qualifier;
        else qualifier = qualifier + "." + fieldAttribute.Qualifier;

        if (qualifier.Contains("PingDisplay"))
        {
            bool b = true;
        }
        
        string? defaultValue = containingField.GetValue(null) as string;
        string translation = localizer.Translate(qualifier, defaultValue ?? $"<{qualifier}>", false);
        containingField.SetValue(null, translation);
    }
}
