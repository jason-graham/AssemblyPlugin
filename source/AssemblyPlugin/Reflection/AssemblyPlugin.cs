//---------------------------------------------------------------------------- 
//
//  Copyright (C) CSharp Labs.  All rights reserved.
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
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization;

    /// <summary>
    /// Provides the ability to load and unload an assembly across an
    /// application domain.
    /// </summary>
    public sealed class AssemblyPlugin : IDisposable
    {
        #region Private Classes
        /// <summary>
        /// Wraps an <see cref="System.Reflection.Assembly"/> allowing creating
        /// object instances and invoking delegates across an application domain boundary.
        /// </summary>
        private class PrivateRemoteAssembly : MarshalByRefObject
        {
            #region Private Classes
            /// <summary>
            /// Handles loading assemblies and references.
            /// </summary>
            private sealed class PrivateRemoteAssemblyLoader : IDisposable
            {
                #region Readonly Fields
                /// <summary>
                /// Defines a collection of loaded assembly references with the full name or path
                /// as the key.
                /// </summary>
                private readonly Dictionary<string, Assembly> _Assemblies = new Dictionary<string, Assembly>();

                /// <summary>
                /// Defines the directory the current assembly is being loaded from.
                /// </summary>
                private string _CurrentAssemblyDirectory;
                #endregion

                #region Constructor
                /// <summary>
                /// Initializes the assembly loader.
                /// </summary>
                public PrivateRemoteAssemblyLoader()
                {
                    //subscribe to AssemblyResolve to manually load references
                    AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
                }
                #endregion

                #region Methods
                /// <summary>
                /// Resolves the assembly reference.
                /// </summary>
                /// <param name="sender"></param>
                /// <param name="args"></param>
                /// <returns>The loaded assembly.</returns>
                private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
                {
                    //get AssemblyName
                    AssemblyName assemblyName = new AssemblyName(args.Name);
                    //get path to assembly
                    string assemblyPath = Path.Combine(_CurrentAssemblyDirectory, assemblyName.Name + ".dll");

                    //check if assembly exists
                    if (File.Exists(assemblyPath))
                        //load assembly
                        return LoadAssembly(assemblyPath);

                    //could not load assembly
                    return null;
                }

                /// <summary>
                /// Loads an assembly given it's file name or path.
                /// </summary>
                /// <param name="assemblyFile">The name or path of the file that contains the manifest of the assembly.</param>
                /// <returns>The loaded assembly.</returns>
                public Assembly LoadAssembly(string assemblyFile)
                {
                    //get the directory the assembly is located in
                    string assemblyDirectory = Path.GetDirectoryName(assemblyFile);

                    //load the assembly
                    Assembly assembly = Assembly.LoadFrom(assemblyFile);

                    //check if new assembly
                    if (!_Assemblies.ContainsKey(assembly.FullName))
                    {
                        //add new assembly to cache
                        _Assemblies.Add(assembly.FullName, assembly);

                        //new assembly, load all references
                        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                        {
                            //temp the current directory
                            string lastAssemblyFile = _CurrentAssemblyDirectory;

                            //set new current directory
                            _CurrentAssemblyDirectory = assemblyDirectory;

                            try
                            {
                                //load assembly and assembly references
                                LoadAssemblyWithReferences(reference);
                            }
                            finally
                            {
                                //reset current directory
                                _CurrentAssemblyDirectory = lastAssemblyFile;
                            }
                        }

                        //return new reference
                        return assembly;
                    }
                    else
                        //assembly already added, return reference
                        return _Assemblies[assembly.FullName];
                }

                /// <summary>
                /// Loads an assembly given it's <see cref="System.Reflection.AssemblyName"/>.
                /// </summary>
                /// <param name="assemblyFile">The name or path of the file that contains the manifest of the assembly.</param>
                /// <returns>The loaded assembly.</returns>
                public void LoadAssemblyWithReferences(AssemblyName assemblyReference)
                {
                    //loads the assembly
                    Assembly assembly = Assembly.Load(assemblyReference);

                    //check if new assembly
                    if (!_Assemblies.ContainsKey(assemblyReference.FullName))
                    {
                        //add new assembly to cache
                        _Assemblies.Add(assemblyReference.FullName, assembly);

                        //new assembly, load all references
                        foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                            LoadAssemblyWithReferences(reference);
                    }
                }
                #endregion

                #region Disposing
                ~PrivateRemoteAssemblyLoader()
                {
                    Dispose(false);
                }

                /// <summary>
                /// Clean up any resources being used.
                /// </summary>
                public void Dispose()
                {
                    Dispose(true);
                    GC.SuppressFinalize(this);
                }

                /// <summary>
                /// Clean up any resources being used.
                /// </summary>
                /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
                private void Dispose(bool disposing)
                {
                    if (disposing)
                        AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
                }
                #endregion
            }
            #endregion

            #region Readonly Fields
            /// <summary>
            /// Defines the assembly plugin.
            /// </summary>
            private readonly Assembly _Assembly;
            #endregion

            #region Constructor
            /// <summary>
            /// Initializes the remote assembly given it's file name or path.
            /// </summary>
            public PrivateRemoteAssembly(string assemblyFile)
            {
                //create the remote assembly loader
                using (PrivateRemoteAssemblyLoader loader = new PrivateRemoteAssemblyLoader())
                    //load the assembly
                    _Assembly = loader.LoadAssembly(assemblyFile);
            }
            #endregion

            #region Methods
            /// <summary>
            /// Locates the specified type from this assembly and creates an instance of
            /// it using the system activator, with optional case-sensitive search and having
            /// the specified arguments.
            /// </summary>
            /// <typeparam name="TReturn">The type of object to return where TReturn : MarshalByRefObject.</typeparam>
            /// <param name="typeName">The <see cref="System.Type.FullName"/> of the type to locate.</param>
            /// <param name="args">An array that contains the arguments to be passed to the constructor. This
            /// array of arguments must match in number, order, and type the parameters of
            /// the constructor to be invoked. If the default constructor is desired, args
            /// must be an empty array or null.</param>
            /// <returns>An instance of the specified type, or null if typeName is not found. The
            /// supplied arguments are used to resolve the type, and to bind the constructor
            /// that is used to create the instance.</returns>
            /// <exception cref="System.ArgumentException">typeName is an empty string ("") or a string beginning with a null character.
            /// -or-The current assembly was loaded into the reflection-only context.</exception>
            /// <exception cref="System.ArgumentNullException">typeName is null.</exception>
            /// <exception cref="System.MissingMethodException">No matching constructor was found.</exception>
            /// <exception cref="System.NotSupportedException">A non-empty activation attributes array is passed to a type that does not
            /// inherit from System.MarshalByRefObject.</exception>
            /// <exception cref="System.IO.FileNotFoundException">typeName requires a dependent assembly that could not be found.</exception>
            /// <exception cref="System.IO.FileLoadException">typeName requires a dependent assembly that was found but could not be loaded.-or-The
            /// current assembly was loaded into the reflection-only context, and typeName
            /// requires a dependent assembly that was not preloaded.</exception>
            /// <exception cref="System.BadImageFormatException">typeName requires a dependent assembly, but the file is not a valid assembly.
            /// -or-typeName requires a dependent assembly which was compiled for a version
            /// of the runtime later than the currently loaded version.</exception>
            public TReturn CreateInstance<TReturn>(string typeName, params object[] args) where TReturn : MarshalByRefObject
            {
                //create and return instance
                return (TReturn)_Assembly.CreateInstance(
                    typeName,
                    false,
                    BindingFlags.CreateInstance,
                    null,
                    args,
                    null,
                    null);
            }

            /// <summary>
            /// Invokes the specified <see cref="System.Action"/> on the <see cref="System.AppDomain"/>
            /// this was created on.
            /// </summary>
            /// <param name="method">The method to invoke.</param>
            public void Invoke(Action method)
            {
                method.Invoke();
            }

            /// <summary>
            /// Invokes the specified <see cref="System.Func&lt;TResult&gt;"/> on the <see cref="System.AppDomain"/>
            /// this was created on.
            /// </summary>
            /// <typeparam name="TResult"></typeparam>
            /// <param name="method">The method to invoke.</param>
            public TResult Invoke<TResult>(Func<TResult> function)
            {
                return function.Invoke();
            }

            /// <summary>
            /// Searches the assembly for types that derive from <typeparamref name="TBase"/>.
            /// </summary>
            /// <typeparam name="TBase">The type of objects to locate where TReturn : MarshalByRefObject.</typeparam>
            /// <returns>An array of identifiers that derive from <typeparamref name="TBase"/>.</returns>
            public PluginTypeIdentifier[] GetTypes<TBase>() where TBase : MarshalByRefObject
            {
                List<PluginTypeIdentifier> plugins = new List<PluginTypeIdentifier>();

                Type tbase = typeof(TBase);

                //enumerate all types
                foreach (Type t in _Assembly.GetTypes())
                {
                    //look for type that derives from tbase
                    if (t.IsSubclassOf(tbase))
                        //add type
                        plugins.Add(new PluginTypeIdentifier(t, tbase));
                }

                //return array of identifiers
                return plugins.ToArray();
            }
            #endregion
        }
        #endregion

        #region Readonly Fields
        /// <summary>
        /// Defines the application domain the assembly is loaded in.
        /// </summary>
        private readonly AppDomain _Domain;

        /// <summary>
        /// Defines the assembly loaded in another application domain.
        /// </summary>
        private readonly PrivateRemoteAssembly _Plugin;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes the plugin from an assembly given it's file name or path.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">assemblyFile is null.</exception>
        /// <exception cref="System.IO.FileNotFoundException">assemblyFile is not found, or the module you are trying to load does not specify a filename extension.</exception>
        /// <exception cref="System.IO.FileLoadException">A file that was found could not be loaded.</exception>
        /// <exception cref="System.BadImageFormatException">assemblyFile is not a valid assembly.</exception>
        /// <exception cref="System.ArgumentException">The assemblyFile parameter is an empty string ("").</exception>
        /// <exception cref="System.IO.PathTooLongException">The assembly name is too long.</exception>
        /// <exception cref="System.IO.FileNotFoundException">An assembly was not found.</exception>
        public AssemblyPlugin(string assemblyFile)
        {
            if (assemblyFile == null)
                throw new ArgumentNullException("assemblyFile");

            //get current domain
            AppDomain current = AppDomain.CurrentDomain;
            //get existing setup
            AppDomainSetup currentSetup = current.SetupInformation;
            //setup domain settings from existing setup
            AppDomainSetup setup = new AppDomainSetup
            {
                ApplicationBase = currentSetup.ApplicationBase,
                PrivateBinPath = currentSetup.PrivateBinPath,
                PrivateBinPathProbe = currentSetup.PrivateBinPathProbe
            };

            //create the new domain with a random yet relevant FriendlyName
            _Domain = AppDomain.CreateDomain(
                string.Format("{0} - AssemblyPluginContainer[{1}]", current.FriendlyName, Guid.NewGuid()),
                null,
                setup);

            try
            {
                //attempts to create the plugin
                _Plugin = CreateEntity<PrivateRemoteAssembly>(assemblyFile);
            }
            catch
            {
                //if the assembly failed to load, unload the domain
                AppDomain.Unload(_Domain);
                //rethrow the exception
                throw;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Locates the specified type from this assembly and creates an instance of
        /// it using the system activator, with optional case-sensitive search and having
        /// the specified arguments.
        /// </summary>
        /// <typeparam name="TReturn">The type of object to return where TReturn : MarshalByRefObject.</typeparam>
        /// <param name="typeName">The <see cref="System.Type.FullName"/> of the type to locate.</param>
        /// <param name="args">An array that contains the arguments to be passed to the constructor. This
        /// array of arguments must match in number, order, and type the parameters of
        /// the constructor to be invoked. If the default constructor is desired, args
        /// must be an empty array or null.</param>
        /// <returns>An instance of the specified type, or null if typeName is not found. The
        /// supplied arguments are used to resolve the type, and to bind the constructor
        /// that is used to create the instance.</returns>
        /// <exception cref="System.ArgumentException">typeName is an empty string ("") or a string beginning with a null character.
        /// -or-The current assembly was loaded into the reflection-only context.</exception>
        /// <exception cref="System.ArgumentNullException">typeName is null.</exception>
        /// <exception cref="System.MissingMethodException">No matching constructor was found.</exception>
        /// <exception cref="System.NotSupportedException">A non-empty activation attributes array is passed to a type that does not
        /// inherit from System.MarshalByRefObject.</exception>
        /// <exception cref="System.IO.FileNotFoundException">typeName requires a dependent assembly that could not be found.</exception>
        /// <exception cref="System.IO.FileLoadException">typeName requires a dependent assembly that was found but could not be loaded.-or-The
        /// current assembly was loaded into the reflection-only context, and typeName
        /// requires a dependent assembly that was not preloaded.</exception>
        /// <exception cref="System.BadImageFormatException">typeName requires a dependent assembly, but the file is not a valid assembly.
        /// -or-typeName requires a dependent assembly which was compiled for a version
        /// of the runtime later than the currently loaded version.</exception>
        /// <exception cref="ArgumentException">One or more argument in <paramref name="args"/> is not serializable.</exception>
        public TReturn CreateInstance<TReturn>(string typeName, params object[] args) where TReturn : MarshalByRefObject
        {
            //all arguments must be serializable
            if (args != null)
            {
                //check each argument
                for (int i = 0; i < args.Length; i++)
                {
                    object obj = args[i];

                    if (obj != null)
                    {
                        //get the type
                        Type t = obj.GetType();

                        //check if has SerializableAttribute or implements ISerializable
                        if (!t.IsSerializable || !typeof(ISerializable).IsAssignableFrom(t))
                            //throw if not serializable
                            throw new ArgumentException(string.Format("Argument {0} type {1} must be serializable.", i, t), "args");
                    }
                }
            }

            //create and return instance
            return _Plugin.CreateInstance<TReturn>(typeName, args);
        }

        /// <summary>
        /// Invokes the specified <see cref="System.Action"/> on the <see cref="System.AppDomain"/>
        /// the plugin assembly was loaded on.
        /// </summary>
        /// <param name="method">The method to invoke.</param>
        public void Invoke(Action method)
        {
            _Plugin.Invoke(method);
        }

        /// <summary>
        /// Invokes the specified <see cref="System.Func&lt;TResult&gt;"/> on the <see cref="System.AppDomain"/>
        /// the plugin assembly was loaded on.
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="method">The method to invoke.</param>
        public TResult Invoke<TResult>(Func<TResult> function)
        {
            return _Plugin.Invoke(function);
        }

        /// <summary>
        /// Creates a new instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of entity to create.</typeparam>
        /// <param name="args">The arguments to pass to the constructor.</param>
        /// <returns>An instance of the object specified by typeName.</returns>
        private T CreateEntity<T>(params object[] args) where T : MarshalByRefObject
        {
            Type type = typeof(T);

            return (T)_Domain.CreateInstanceAndUnwrap(
                type.Assembly.FullName,
                type.FullName,
                false,
                BindingFlags.CreateInstance,
                null,
                args,
                null,
                null);
        }

        /// <summary>
        /// Searches the assembly for types that derive from <typeparamref name="TBase"/>.
        /// </summary>
        /// <typeparam name="TBase">The type of objects to locate where TReturn : MarshalByRefObject.</typeparam>
        /// <returns>An array of identifiers that derive from <typeparamref name="TBase"/>.</returns>
        public PluginTypeIdentifier[] GetTypes<TBase>() where TBase : MarshalByRefObject
        {
            return _Plugin.GetTypes<TBase>();
        }
        #endregion

        #region Disposing
        ~AssemblyPlugin()
        {
            Dispose(false);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        private void Dispose(bool disposing)
        {
            if (disposing)
                AppDomain.Unload(_Domain);
        }
        #endregion
    }
}
