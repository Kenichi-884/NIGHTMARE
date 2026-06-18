using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace RoslynCSharp.Project
{
    internal sealed class CSharpProjectParser
    {
        // Private
        private readonly CSharpProject project;

        // Constructor
        public CSharpProjectParser(CSharpProject project)
        {
            // Check for null
            if(project == null)
                throw new ArgumentNullException(nameof(project));

            this.project = project; 
        }

        // Methods
        public void Parse(XmlReader reader)
        {
            // Load the document
            XDocument doc = XDocument.Load(reader);

            // Get scheme
            XNamespace scheme = ParseScheme(doc);

            // Parse assembly name
            if (ParseAssemblyName(doc) == false)
                return;

            // Parse sources
            if (ParseSourceFiles(doc, scheme) == false)
                return;

            // Parse defines
            if(ParseDefineSymbols(doc, scheme) == false) 
                return;

            // Parse assembly references
            if(ParseAssemblyReferences(doc, scheme) == false) 
                return;

            // Parse project references
            if(ParseProjectReferences(doc, scheme) == false) 
                return;
        }

        private string ParseScheme(XDocument doc)
        {
            try
            {
                return doc.Root
                    .Attribute("xmlns")?
                    .Value;
            }
            catch
            {
                return string.Empty;
            }
        }

        private bool ParseAssemblyName(XDocument doc)
        {
            try
            {
                // Update assembly name
                project.AssemblyName = doc
                    .Descendants()
                    .SingleOrDefault(r => r.Name.LocalName == "AssemblyName")?
                    .Value;
            }
            catch(Exception e)
            {
                project.ParseException = e;
                return false;
            }
            return true;
        }

        private bool ParseSourceFiles(XDocument doc, XNamespace scheme)
        {
            try
            {
                // Find source files
                foreach (string sourceFile in doc
                    .Element(scheme + "Project")
                    .Elements(scheme + "ItemGroup")
                    .Elements(scheme + "Compile")
                    .Select(s => s.FirstAttribute.Value))
                {
                    // Add the source file
                    project.Sources.Add(sourceFile);
                }
            }
            catch (Exception e)
            {
                project.ParseException = e;
                return false;
            }
            return true;
        }

        private bool ParseDefineSymbols(XDocument doc, XNamespace scheme)
        {
            try
            {
                // Find define symbols files
                foreach (string defineSymbol in doc
                    .Element(scheme + "Project")
                    .Elements(scheme + "PropertyGroup")
                    .Elements(scheme + "DefineConstants")
                    .Select(d => d.Value))
                {
                    // Split the symbol
                    string[] splitDefines = defineSymbol.Split(';');

                    // Add the define symbol
                    foreach(string define in splitDefines)
                        project.DefineSymbols.Add(define);
                }
            }
            catch (Exception e)
            {
                project.ParseException = e;
                return false;
            }
            return true;
        }

        private bool ParseAssemblyReferences(XDocument doc, XNamespace scheme)
        {
            try
            {
                // Find all references
                foreach(string reference in doc
                    .Element(scheme + "Project")
                    .Elements(scheme + "ItemGroup")
                    .Elements(scheme + "Reference")
                    .Select(r => string.IsNullOrEmpty(r.Value) 
                        ? r.FirstAttribute.Value
                        : r.Value)
                    .Select(r => r
                        .Trim(' ', '\t', '"', '\n')
                        .Split('\n')[0]))
                {
                    // Add the reference
                    project.AssemblyReferences.Add(reference);
                }
            }
            catch(Exception e)
            {
                project.ParseException = e; 
                return false;
            }
            return true;
        }

        private bool ParseProjectReferences(XDocument doc, XNamespace scheme)
        {
            try
            {
                // Find all references
                foreach (string reference in doc
                    .Element(scheme + "Project")
                    .Elements(scheme + "ItemGroup")
                    .Elements(scheme + "ProjectReference")
                    .Select(r => r.FirstAttribute.Value))
                {
                    // Add the reference
                    project.ProjectReferences.Add(reference);
                }
            }
            catch (Exception e)
            {
                project.ParseException = e;
                return false;
            }
            return true;
        }
    }
}
