﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace ProductiveRage.Immutable.Analyser {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "15.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("ProductiveRage.Immutable.Analyser.Resources", typeof(Resources).GetTypeInfo().Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet should only be used in specific circumstances.
        /// </summary>
        internal static string CtorAnalyserTitle {
            get {
                return ResourceManager.GetString("CtorAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet&apos;s propertyRetriever lambda must directly indicate an instance property that has no Bridge attributes on the getter or setter.
        /// </summary>
        internal static string CtorBridgeAttributeMessageFormat {
            get {
                return ResourceManager.GetString("CtorBridgeAttributeMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet&apos;s propertyRetriever lambda must directly indicate an instance property with a getter and a setter (which may be private) or that is a readonly auto-property.
        /// </summary>
        internal static string CtorDirectPropertyTargetAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("CtorDirectPropertyTargetAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet should only be called within a constructor.
        /// </summary>
        internal static string CtorMayOnlyBeCalledWithConstructorMessageFormat {
            get {
                return ResourceManager.GetString("CtorMayOnlyBeCalledWithConstructorMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet must be a simple member access expression that targets &quot;this&quot; - it must be of the form this.CtorSet(..).
        /// </summary>
        internal static string CtorSimpleMemberAccessRuleMessageFormat {
            get {
                return ResourceManager.GetString("CtorSimpleMemberAccessRuleMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to CtorSet&apos;s propertyRetriever lambda must directly indicate an instance property with a getter and a setter (which may be private) or that is a readonly auto-property.
        /// </summary>
        internal static string CtorSimplePropertyAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("CtorSimplePropertyAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to GetProperty should only be used in a particular manner.
        /// </summary>
        internal static string GetPropertyAnalyserTitle {
            get {
                return ResourceManager.GetString("GetPropertyAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to GetProperty&apos;s propertyRetriever lambda must directly indicate an instance property that has no Bridge attributes on the getter or setter.
        /// </summary>
        internal static string GetPropertyBridgeAttributeMessageFormat {
            get {
                return ResourceManager.GetString("GetPropertyBridgeAttributeMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to GetProperty&apos;s propertyRetriever lambda&apos;s target must be a direct access and may not include any casts or other indirection.
        /// </summary>
        internal static string GetPropertyDirectPropertyTargetAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("GetPropertyDirectPropertyTargetAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to GetProperty&apos;s propertyRetriever lambda must directly indicate an instance property with a getter and a setter (which may be private) or that is a readonly auto-property.
        /// </summary>
        internal static string GetPropertySimplePropertyAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("GetPropertySimplePropertyAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Properties on IAmImmutable must follow prescribed guidelines.
        /// </summary>
        internal static string IAmImmutableAnalyserTitle {
            get {
                return ResourceManager.GetString("IAmImmutableAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IAmImmutable implementation &apos;{0}&apos; has an empty constructor that may be used to populate the class.
        /// </summary>
        internal static string IAmImmutableAutoPopulatorAnalyserEmptyConstructorMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutableAutoPopulatorAnalyserEmptyConstructorMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IAmImmutable implementation &apos;{0}&apos; has constructor argument(s) that are not used to set properties: {1}.
        /// </summary>
        internal static string IAmImmutableAutoPopulatorAnalyserOutOfSyncConstructorMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutableAutoPopulatorAnalyserOutOfSyncConstructorMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IAmImmutable implementation may be auto-generated.
        /// </summary>
        internal static string IAmImmutableAutoPopulatorAnalyserTitle {
            get {
                return ResourceManager.GetString("IAmImmutableAutoPopulatorAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to IAmImmutable implementations may not have non-readonly public fields (such as {0}).
        /// </summary>
        internal static string IAmImmutableFieldsMayNotBePublicAndMutableMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutableFieldsMayNotBePublicAndMutableMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The setter on property &apos;{0}&apos; must be private as it is on a class that implements IAmImmutable.
        /// </summary>
        internal static string IAmImmutablePropertiesMayNotHavePublicSetterMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutablePropertiesMayNotHavePublicSetterMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Property &apos;{0}&apos; must have a setter since it has a getter and is on a class that implements IAmImmutable.
        /// </summary>
        internal static string IAmImmutablePropertiesMustHaveSettersMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutablePropertiesMustHaveSettersMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to May not have Bridge attributes on properties with getters ({0}) on classes that implement IAmImmutable.
        /// </summary>
        internal static string IAmImmutablePropertiesMustNotHaveBridgeAttributesMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutablePropertiesMustNotHaveBridgeAttributesMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to When new IAmImmutable instances are created via &quot;With&quot; calls, the constructor is not called and so any validation there will be bypassed. If parameter validation is required then you may add a &quot;Validate&quot; method to the class and call it at the end of the constructor, it will also be called whenever &quot;With&quot;creates a new instance. The &quot;Validate&quot; method must have no parameters and must not be decorated with any Bridge attributes (it is acceptable for the method to be private)..
        /// </summary>
        internal static string IAmImmutableValidationShouldNotBePerformedInConstructorMessageFormat {
            get {
                return ResourceManager.GetString("IAmImmutableValidationShouldNotBePerformedInConstructorMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The propertyRetriever must be a simple in-place property-accessing lambda or a PropertyIdentifier&lt;T, TPropertyValue&gt; instance or a method argument with a [PropertyIdentifier] attribute - here it is a method argument that is a delegate type but without a [PropertyIdentifier] attribute, did you forget to add it?.
        /// </summary>
        internal static string MethodParameterWithoutPropertyIdentifierAttribute {
            get {
                return ResourceManager.GetString("MethodParameterWithoutPropertyIdentifierAttribute", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] arguments must follow prescribed guidelines.
        /// </summary>
        internal static string PropertyIdentifierAttributeAnalyserTitle {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] argument lambdas must directly indicate an instance property that has no Bridge attributes on the getter or setter.
        /// </summary>
        internal static string PropertyIdentifierAttributeBridgeAttributeMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeBridgeAttributeMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] argument lambdas&apos; targets must be direct accesses and may not include any casts or other indirection.
        /// </summary>
        internal static string PropertyIdentifierAttributeDirectPropertyTargetAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeDirectPropertyTargetAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] arguments must be a delegate that takes a single source reference and returns a property value from it (so it must be a two-argument delegate type).
        /// </summary>
        internal static string PropertyIdentifierAttributeInvalidDelegateMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeInvalidDelegateMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] arguments may not be reassigned (they may not be set directly to a new value and they may not be used to provide a ref or out argument in a method call).
        /// </summary>
        internal static string PropertyIdentifierAttributeReassignmentMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeReassignmentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to [PropertyIdentifier] argument lambdas must directly indicate an instance property with a getter and a setter (which may be private) or that is a readonly auto-property.
        /// </summary>
        internal static string PropertyIdentifierAttributeSimplePropertyAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeSimplePropertyAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The target type of the delegate is not specific enough - the specified property is of type &quot;{0}&quot; and so the delegate return type must be this type or one derived from it (which &quot;{1}&quot; is not).
        /// </summary>
        internal static string PropertyIdentifierAttributeTargetTypeNotSpecificEnoughMessageFormat {
            get {
                return ResourceManager.GetString("PropertyIdentifierAttributeTargetTypeNotSpecificEnoughMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Properties annotated with [ReadOnly] may only be targeted by &quot;CtorSet&quot; (not &quot;With&quot; or &quot;GetProperty&quot;and are not elligible as [PropertyIdentifier] values).
        /// </summary>
        internal static string ReadOnlyPropertyAccessedMessageFormat {
            get {
                return ResourceManager.GetString("ReadOnlyPropertyAccessedMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to TPropertyValue is a type that is less specialised than the indicated property - the specified property is of type &quot;{0}&quot; and so the type of the value that will be used to update the property must be this type or one derived from it (which &quot;{1}&quot; is not).
        /// </summary>
        internal static string TPropertyValueNotSpecificEnoughMessageFormat {
            get {
                return ResourceManager.GetString("TPropertyValueNotSpecificEnoughMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to With should only be used in a particular manner.
        /// </summary>
        internal static string WithAnalyserTitle {
            get {
                return ResourceManager.GetString("WithAnalyserTitle", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to With&apos;s propertyRetriever lambda must directly indicate an instance property that has no Bridge attributes on the getter or setter.
        /// </summary>
        internal static string WithBridgeAttributeMessageFormat {
            get {
                return ResourceManager.GetString("WithBridgeAttributeMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to With&apos;s propertyRetriever lambda&apos;s target must be a direct access and may not include any casts or other indirection.
        /// </summary>
        internal static string WithDirectPropertyTargetAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("WithDirectPropertyTargetAccessorArgumentMessageFormat", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to With&apos;s propertyRetriever lambda must directly indicate an instance property with a getter and a setter (which may be private) or that is a readonly auto-property.
        /// </summary>
        internal static string WithSimplePropertyAccessorArgumentMessageFormat {
            get {
                return ResourceManager.GetString("WithSimplePropertyAccessorArgumentMessageFormat", resourceCulture);
            }
        }
    }
}
