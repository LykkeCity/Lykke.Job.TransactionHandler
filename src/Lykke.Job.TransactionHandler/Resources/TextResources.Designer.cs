﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Lykke.Job.TransactionHandler.Resources {
    using System;
    using System.Reflection;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class TextResources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal TextResources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Lykke.Job.TransactionHandler.Resources.TextResources", typeof(TextResources).GetTypeInfo().Assembly);
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
        ///   Looks up a localized string similar to Credited {0} {1}.
        /// </summary>
        internal static string CreditedPushText {
            get {
                return ResourceManager.GetString("CreditedPushText", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} cancelled
        ///Price {3} {4}.
        /// </summary>
        internal static string LimitOrderCancelled {
            get {
                return ResourceManager.GetString("LimitOrderCancelled", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} executed
        ///Price {3} {4} volume {5} {6}.
        /// </summary>
        internal static string LimitOrderExecuted {
            get {
                return ResourceManager.GetString("LimitOrderExecuted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} expired
        ///Price {3} {4}.
        /// </summary>
        internal static string LimitOrderExpired {
            get {
                return ResourceManager.GetString("LimitOrderExpired", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} partially executed
        ///Price {3} {4} volume {5} {6}.
        /// </summary>
        internal static string LimitOrderPartiallyExecuted {
            get {
                return ResourceManager.GetString("LimitOrderPartiallyExecuted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} rejected
        ///Price {3} {4}.
        /// </summary>
        internal static string LimitOrderRejected {
            get {
                return ResourceManager.GetString("LimitOrderRejected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Limit Order for {0} {1} {2} placed
        ///Price {3} {4}.
        /// </summary>
        internal static string LimitOrderStarted {
            get {
                return ResourceManager.GetString("LimitOrderStarted", resourceCulture);
            }
        }
    }
}
