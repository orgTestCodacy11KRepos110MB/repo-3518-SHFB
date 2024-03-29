/********************************************************************************************

Copyright (c) Microsoft Corporation 
All rights reserved. 

Microsoft Public License: 

This license governs use of the accompanying software. If you use the software, you 
accept this license. If you do not accept the license, do not use the software. 

1. Definitions 
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the 
same meaning here as under U.S. copyright law. 
A "contribution" is the original software, or any additions or changes to the software. 
A "contributor" is any person that distributes its contribution under this license. 
"Licensed patents" are a contributor's patent claims that read directly on its contribution. 

2. Grant of Rights 
(A) Copyright Grant- Subject to the terms of this license, including the license conditions 
and limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
royalty-free copyright license to reproduce its contribution, prepare derivative works of 
its contribution, and distribute its contribution or any derivative works that you create. 
(B) Patent Grant- Subject to the terms of this license, including the license conditions 
and limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
royalty-free license under its licensed patents to make, have made, use, sell, offer for 
sale, import, and/or otherwise dispose of its contribution in the software or derivative 
works of the contribution in the software. 

3. Conditions and Limitations 
(A) No Trademark License- This license does not grant you rights to use any contributors' 
name, logo, or trademarks. 
(B) If you bring a patent claim against any contributor over patents that you claim are 
infringed by the software, your patent license from such contributor to the software ends 
automatically. 
(C) If you distribute any portion of the software, you must retain all copyright, patent, 
trademark, and attribution notices that are present in the software. 
(D) If you distribute any portion of the software in source code form, you may do so only 
under this license by including a complete copy of this license with your distribution. 
If you distribute any portion of the software in compiled or object code form, you may only 
do so under a license that complies with this license. 
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give 
no express warranties, guarantees or conditions. You may have additional consumer rights 
under your local laws which this license cannot change. To the extent permitted under your 
local laws, the contributors exclude the implied warranties of merchantability, fitness for 
a particular purpose and non-infringement.

********************************************************************************************/

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Microsoft.VisualStudio.Project
{
    /// <summary>
    /// This type of node is used for references to COM components.
    /// </summary>
    [CLSCompliant(false)]
    [ComVisible(true)]
    public class ComReferenceNode : ReferenceNode
    {
        #region fields
        private string typeName;
        private Guid typeGuid;
        private string projectRelativeFilePath;
        private string installedFilePath;
        private string minorVersionNumber;
        private string majorVersionNumber;
        private string lcid;
        #endregion

        #region properties
        public override string Caption
        {
            get { return this.typeName; }
        }

        public override string Url
        {
            get
            {
                return this.projectRelativeFilePath;
            }
        }

        /// <summary>
        /// Returns the Guid of the COM object.
        /// </summary>
        public Guid TypeGuid
        {
            get { return this.typeGuid; }
        }

        /// <summary>
        /// Returns the path where the COM object is installed.
        /// </summary>
        public string InstalledFilePath
        {
            get { return this.installedFilePath; }
        }

        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "LCID")]
        public string LCID
        {
            get { return lcid; }
        }

        public int MajorVersionNumber
        {
            get
            {
                if(string.IsNullOrEmpty(majorVersionNumber))
                {
                    return 0;
                }
                return int.Parse(majorVersionNumber, CultureInfo.CurrentCulture);
            }
        }

        public bool EmbedInteropTypes
        {
            get
            {
                bool value;
                bool.TryParse(this.ItemNode.GetMetadata(ProjectFileConstants.EmbedInteropTypes), out value);
                return value;
            }

            set
            {
                this.ItemNode.SetMetadata(ProjectFileConstants.EmbedInteropTypes, value.ToString());
            }
        }

        public string WrapperTool
        {
            get { return this.ItemNode.GetMetadata(ProjectFileConstants.WrapperTool); }
            set { this.ItemNode.SetMetadata(ProjectFileConstants.WrapperTool, value); }
        }

        public int MinorVersionNumber
        {
            get
            {
                if(string.IsNullOrEmpty(minorVersionNumber))
                {
                    return 0;
                }
                return int.Parse(minorVersionNumber, CultureInfo.CurrentCulture);
            }
        }
        private Automation.OAComReference comReference;
        internal override object Object
        {
            get
            {
                if(null == comReference)
                {
                    comReference = new Automation.OAComReference(this);
                }
                return comReference;
            }
        }
        #endregion

        #region ctors
        /// <summary>
        /// Constructor for the ComReferenceNode. 
        /// </summary>
        public ComReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element)
        {
            this.typeName = this.ItemNode.GetMetadata(ProjectFileConstants.Include);
            string typeGuidAsString = this.ItemNode.GetMetadata(ProjectFileConstants.Guid);
            if(typeGuidAsString != null)
            {
                this.typeGuid = new Guid(typeGuidAsString);
            }

            this.majorVersionNumber = this.ItemNode.GetMetadata(ProjectFileConstants.VersionMajor);
            this.minorVersionNumber = this.ItemNode.GetMetadata(ProjectFileConstants.VersionMinor);
            this.lcid = this.ItemNode.GetMetadata(ProjectFileConstants.Lcid);
            this.SetProjectItemsThatRelyOnReferencesToBeResolved(false);
            this.SetInstalledFilePath();
        }

        /// <summary>
        /// Overloaded constructor for creating a ComReferenceNode from selector data
        /// </summary>
        /// <param name="root">The Project node</param>
        /// <param name="selectorData">The component selctor data.</param>
        /// <param name="wrapperTool">The wrapper tool</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")]
        public ComReferenceNode(ProjectNode root, VSCOMPONENTSELECTORDATA selectorData, string wrapperTool = null)
            : base(root)
        {
            if(root == null)
            {
                throw new ArgumentNullException("root");
            }
            if(selectorData.type == VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project
                || selectorData.type == VSCOMPONENTTYPE.VSCOMPONENTTYPE_ComPlus)
            {
                throw new ArgumentException("SelectorData cannot be of type VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project or VSCOMPONENTTYPE.VSCOMPONENTTYPE_ComPlus", "selectorData");
            }

            // Initialize private state
            this.typeName = selectorData.bstrTitle;
            this.typeGuid = selectorData.guidTypeLibrary;
            this.majorVersionNumber = selectorData.wTypeLibraryMajorVersion.ToString(CultureInfo.InvariantCulture);
            this.minorVersionNumber = selectorData.wTypeLibraryMinorVersion.ToString(CultureInfo.InvariantCulture);
            this.lcid = selectorData.lcidTypeLibrary.ToString(CultureInfo.InvariantCulture);
            this.WrapperTool = wrapperTool;

            // Check to see if the COM object actually exists.
            this.SetInstalledFilePath();
            // If the value cannot be set throw.
            if(String.IsNullOrEmpty(this.installedFilePath))
            {
                throw new InvalidOperationException();
            }
        }
        #endregion

        #region methods
        protected override NodeProperties CreatePropertiesObject()
        {
            return new ComReferenceProperties(this);
        }
        
        /// <summary>
        /// Links a reference node to the project and hierarchy.
        /// </summary>
        protected override void BindReferenceData()
        {
            Debug.Assert(this.ItemNode != null, "The AssemblyName field has not been initialized");

            // We need to create the project element at this point if it has not been created.
            // We cannot do that from the ctor if input comes from a component selector data, since had we been doing that we would have added a project element to the project file.  
            // The problem with that approach is that we would need to remove the project element if the item cannot be added to the hierachy (E.g. It already exists).
            // It is just safer to update the project file now. This is the intent of this method.
            // Call MSBuild to build the target ResolveComReferences
            if(this.ItemNode == null || this.ItemNode.Item == null)
            {
                this.ItemNode = this.GetProjectElementBasedOnInputFromComponentSelectorData();
            }

            this.SetProjectItemsThatRelyOnReferencesToBeResolved(true);
        }

        /// <summary>
        /// Checks if a reference is already added. The method parses all references and compares the the FinalItemSpec and the Guid.
        /// </summary>
        /// <returns>true if the assembly has already been added.</returns>
        protected internal override bool IsAlreadyAdded(out ReferenceNode existingReference)
        {
            ReferenceContainerNode referencesFolder = this.ProjectMgr.FindChild(ReferenceContainerNode.ReferencesNodeVirtualName) as ReferenceContainerNode;
            Debug.Assert(referencesFolder != null, "Could not find the References node");

            for(HierarchyNode n = referencesFolder.FirstChild; n != null; n = n.NextSibling)
            {
                ComReferenceNode referenceNode = n as ComReferenceNode;

                if(referenceNode != null)
                {
                    // We check if the name and guids are the same
                    if(referenceNode.TypeGuid == this.TypeGuid && String.Compare(referenceNode.Caption, this.Caption, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        existingReference = referenceNode;
                        return true;
                    }
                }
            }

            existingReference = null;
            return false;
        }

        /// <summary>
        /// Determines if this is node a valid node for painting the default reference icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon()
        {
            return !String.IsNullOrEmpty(this.installedFilePath);
        }

        /// <summary>
        /// This is an helper method to convert the VSCOMPONENTSELECTORDATA recieved by the
        /// implementer of IVsComponentUser into a ProjectElement that can be used to create
        /// an instance of this class.
        /// This should not be called for project reference or reference to managed assemblies.
        /// </summary>
        /// <returns>ProjectElement corresponding to the COM component passed in</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase")]
        private ProjectElement GetProjectElementBasedOnInputFromComponentSelectorData()
        {

            ProjectElement element = new ProjectElement(this.ProjectMgr, this.typeName, ProjectFileConstants.COMReference);

            // Set the basic information regarding this COM component
            element.SetMetadata(ProjectFileConstants.Guid, this.typeGuid.ToString("B"));
            element.SetMetadata(ProjectFileConstants.VersionMajor, this.majorVersionNumber);
            element.SetMetadata(ProjectFileConstants.VersionMinor, this.minorVersionNumber);
            element.SetMetadata(ProjectFileConstants.Lcid, this.lcid);
            element.SetMetadata(ProjectFileConstants.Isolated, false.ToString());

            // See if a PIA exist for this component
            TypeLibConverter typelib = new TypeLibConverter();
            string assemblyName;
            string assemblyCodeBase;
            if(typelib.GetPrimaryInteropAssembly(this.typeGuid, Int32.Parse(this.majorVersionNumber, CultureInfo.InvariantCulture), Int32.Parse(this.minorVersionNumber, CultureInfo.InvariantCulture), Int32.Parse(this.lcid, CultureInfo.InvariantCulture), out assemblyName, out assemblyCodeBase))
            {
                element.SetMetadata(ProjectFileConstants.WrapperTool, WrapperToolAttributeValue.Primary.ToString().ToLowerInvariant());
            }
            else
            {
                // MSBuild will have to generate an interop assembly
                element.SetMetadata(ProjectFileConstants.WrapperTool, WrapperToolAttributeValue.TlbImp.ToString().ToLowerInvariant());
                element.SetMetadata(ProjectFileConstants.EmbedInteropTypes, true.ToString());
                element.SetMetadata(ProjectFileConstants.Private, true.ToString());
            }
            return element;
        }

        private void SetProjectItemsThatRelyOnReferencesToBeResolved(bool renameItemNode)
        {
            // Call MSBuild to build the target ResolveComReferences
            bool success;
            ErrorHandler.ThrowOnFailure(this.ProjectMgr.BuildTarget(MsBuildTarget.ResolveComReferences, out success));
            if(!success)
                throw new InvalidOperationException();

            // Now loop through the generated COM References to find the corresponding one
            IEnumerable<ProjectItem> comReferences = this.ProjectMgr.BuildProject.GetItems(MsBuildGeneratedItemType.ComReferenceWrappers);
            foreach (ProjectItem reference in comReferences)
            {
                if(String.Compare(reference.GetMetadataValue(ProjectFileConstants.Guid), this.typeGuid.ToString("B"), StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(reference.GetMetadataValue(ProjectFileConstants.VersionMajor), this.majorVersionNumber, StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(reference.GetMetadataValue(ProjectFileConstants.VersionMinor), this.minorVersionNumber, StringComparison.OrdinalIgnoreCase) == 0
                    && String.Compare(reference.GetMetadataValue(ProjectFileConstants.Lcid), this.lcid, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    string name = reference.EvaluatedInclude;
                    if(Path.IsPathRooted(name))
                    {
                        this.projectRelativeFilePath = name;
                    }
                    else
                    {
                        this.projectRelativeFilePath = Path.Combine(this.ProjectMgr.ProjectFolder, name);
                    }

                    if(renameItemNode)
                    {
                        this.ItemNode.Rename(Path.GetFileNameWithoutExtension(name));
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Verify that the TypeLib is registered and set the the installed file path of the com reference.
        /// </summary>
        /// <returns></returns>
        private void SetInstalledFilePath()
        {
            string registryPath = string.Format(CultureInfo.InvariantCulture, @"TYPELIB\{0:B}\{1:x}.{2:x}", this.typeGuid, this.MajorVersionNumber, this.MinorVersionNumber);
            using(RegistryKey typeLib = Registry.ClassesRoot.OpenSubKey(registryPath))
            {
                if(typeLib != null)
                {
                    // Check if we need to set the name for this type.
                    if(string.IsNullOrEmpty(this.typeName))
                    {
                        this.typeName = typeLib.GetValue(string.Empty) as string;
                    }
                    // Now get the path to the file that contains this type library.
                    using(RegistryKey installKey = typeLib.OpenSubKey(string.Format(CultureInfo.InvariantCulture, @"{0}\win32", this.LCID)))
                    {
                        this.installedFilePath = installKey.GetValue(String.Empty) as String;
                    }
                }
            }
        }

        #endregion
    }
}
