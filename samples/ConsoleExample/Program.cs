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

namespace ConsoleExample
{
    using PluginBaseLibrary;
    using System;
    using System.Reflection;

    class Program
    {
        static void Main(string[] args)
        {
            //loads the demo plugin library
            using (AssemblyPlugin plugin = new AssemblyPlugin("../../../Plugin/bin/debug/Plugin.dll"))
            {
                Console.Write("Searching for types... ");

                //gets an array of types deriving from PluginBaseClass
                PluginTypeIdentifier[] types = plugin.GetTypes<PluginBaseClass>();

                Console.WriteLine(string.Format("{0} found", types.Length));
                Console.WriteLine();

                //print all types out
                foreach (PluginTypeIdentifier id in types)
                {
                    Console.WriteLine(string.Format("Name: {0}", id.Name));
                    Console.WriteLine(string.Format("Full name: {0}", id.FullName));
                    Console.WriteLine(string.Format("Display name: {0}", id.DisplayName));
                    Console.WriteLine(string.Format("Description: {0}", id.Description));
                    Console.WriteLine();
                }

                Console.WriteLine("Testing first plugin with string arguments...");

                //creates instance of Plugin.StringConcat converted to base-type:
                PluginBaseClass instance = plugin.CreateInstance<PluginBaseClass>(types[0].FullName);
                //calls the StringConcat.Process method which concatenates the strings:
                Console.WriteLine(string.Format("Result: {0}", instance.Process("Hello", " ", "World", "!")));
            }

            Console.ReadKey();
        }
    }
}
