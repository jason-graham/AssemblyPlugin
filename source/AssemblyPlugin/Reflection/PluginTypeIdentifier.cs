//---------------------------------------------------------------------------- 
//
//  Copyright (C) Jason Graham.  All rights reserved.
// 
//  Permission is hereby granted, free of charge, to any person obtaining a copy
//  of this software and associated documentation files (the "Software"), to deal
//  in the Software without restriction, including without limitation the rights
//  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  copies of the Software, and to permit persons to whom the Software is
//  furnished to do so, subject to the following conditions:
// 
//  The above copyright notice and this permission notice shall be included in
//  all copies or substantial portions of the Software.
// 
//  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//  THE SOFTWARE.
// 
// History
//  08/11/13    Created 
//
//---------------------------------------------------------------------------

namespace System.Reflection
{
    using System.ComponentModel;

    /// <summary>
    /// Identifies a type in a plugin.
    /// </summary>
    [Serializable]
    public sealed class PluginTypeIdentifier
    {
        /// <summary>
        /// Gets the display name for the plugin.
        /// </summary>
        public string DisplayName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the plugin.
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the fully qualified name of the plugin <see cref="System.Type"/>, including the namespace
        /// of the <see cref="System.Type"/> but not the assembly.
        /// </summary>
        public string FullName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the description of the plugin.
        /// </summary>
        public string Description
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the type the plugin derives from.
        /// </summary>
        public Type BaseType
        {
            get;
            private set;
        }

        /// <summary>
        /// Initializes the plugin identifier from a type.
        /// </summary>
        /// <param name="type">The <see cref="System.Type"/> this identifies.</param>
        /// <param name="baseType">The <see cref="System.Type"/> <paramref name="type"/> derives from.</param>
        public PluginTypeIdentifier(Type type, Type baseType)
        {
            if (type == null)
                throw new ArgumentNullException("type");
            if (baseType == null)
                throw new ArgumentNullException("baseType");

            BaseType = baseType;
            Name = type.Name;
            FullName = type.FullName;
            
            DescriptionAttribute description = type.GetCustomAttribute<DescriptionAttribute>();

            if (description != null)
                Description = description.Description;

            DisplayNameAttribute displayName = type.GetCustomAttribute<DisplayNameAttribute>();

            if (displayName != null)
                DisplayName = displayName.DisplayName; 
        }
    }
}