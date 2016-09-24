Using Assembly Plugins through Application Domains
==================================================

Assemblies can be loaded at runtime, through reflection, using the System.Reflection.Assembly class into the current application domain. Unfortunately, there is no way to unload an assembly without unloading the entire application domain that contains it. To allow loading and unloading an assembly, I have created the AssemblyPlugin class which loads and unloads assemblies in an isolated application domain through remoting.

How it works
------------

When the AssemblyPlugin class is initialized, a dedicated application domain is created to load assembly references, create entities and class instances as well as instantiate an instance of PrivateRemoteAssembly in the new application domain (more on this later):

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
            string.Format("{0} - AssemblyPluginContainer[{1}]", 
	            current.FriendlyName, Guid.NewGuid()),
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

To pass a type between application domains, the type must be serializable. To communicate between application domains, a type must derive from MarshalByRefObject. Types are initially created in the new AppDomain through the CreateEntity method to enable access across application domain boundaries:

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

An instance of PrivateRemoteAssembly is created in the new application domain and is responsible for communicating between application domains. When PrivateRemoteAssembly is initialized, the PrivateRemoteAssemblyLoader is initialized for loading the assembly and all references in the new application domain.

To load all references, a collection of loaded assembly names or paths and assemblies are maintained. The target assembly is loaded and all references are recursively loaded. If any reference fails to load, the AppDomain.CurrentDomain.AssemblyResolve event is fired and the assembly is loaded from file:

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

Actual loading of assemblies is facilitated through two loading methods, the first that loads an assembly from file and the other which loads from the AssemblyName:

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

After all assemblies have been successfully loaded, instances can be created through the AssemblyPlugin.CreateInstance method which reaches across the application domains to return a type that derives from MarshalByRefObject casted as a specified base type.

Available plugin types can be located using the GetTypes method which searches for any type that derives from a specified base type:

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

When the AssemblyPlugin class is disposed, the application domain is unloaded causing the assembly and all references to be unloaded.

Using
-----

Create a base type that derives from MarshalByRefObject and is located in a separate assembly:

    public abstract class PluginBaseClass : MarshalByRefObject
    {
        public abstract object Process(params object[] args);
    }

The plugin base assembly should be referenced by both the application that will instantiate AssemblyPlugin and a plugin assembly. In the plugin assembly, create a type that derives from PluginBaseClass that overrides the Process method:

    [DisplayName("Simple Concatenation")]
    [Description("This class simply concatenates string arguments.")]
    public class StringConcat : PluginBaseClass
    {
        public override object Process(params object[] args)
        {
            //simply concatenates the arguments
            return string.Concat(args);
        }
    }

This allows the AssemblyPlugin class to create and return an instance of a type, casted to the base type that is loaded in both application domains.

All types that are passed between application domains are expected to be marked Serializable or implement ISerializable.

To use the StringConcat type across application domains, initialize the AssemblyPlugin and call the CreateInstance method to return the PluginBaseClass type:

    //loads the demo plugin library
    using (AssemblyPlugin plugin = new AssemblyPlugin("[Your Plugin Assembly].dll"))
    {
        //creates instance of [Your Plugin Namespace].StringConcat converted to base-type:
        PluginBaseClass instance = plugin.CreateInstance<PluginBaseClass>("[Your Plugin Namespace].StringConcat");
        //calls the StringConcat.Process method which concatenates the strings:
        Console.WriteLine(string.Format("Result: {0}", instance.Process("Hello", " ", "World", "!")));
    }

All types that derive from a base type in a plugin can be located using the GetTypes method:

    PluginTypeIdentifier[] types = plugin.GetTypes<PluginBaseClass>();

The PluginTypeIdentifier class provides BaseType, FullName, Name, Description and DisplayName properties to identify available plugin types.
