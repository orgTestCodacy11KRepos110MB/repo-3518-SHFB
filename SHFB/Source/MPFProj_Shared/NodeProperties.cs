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

//===============================================================================================================
// File    : NodeProperties.cs
// Updated : 12/29/2013
// Modifier: Eric Woodruff  (Eric@EWoodruff.us)
//
// This file has been modified to support "Show All Files" in the project.  Search for "!EFW" to find the
// changes.
//
//    Date     Who  Comments
// ==============================================================================================================
// 04/17/2011  EFW  Added code to FileNodeProperties to hide the BuildAction property for non-member nodes.
//                  Also added code to it and FolderNodeProperties to prevent editing of the name for non-member
//                  items.
// 12/29/2013  EFW  Added support for ReferenceOutputAssembly metadata
//===============================================================================================================

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IOleServiceProvider = Microsoft.VisualStudio.OLE.Interop.IServiceProvider;

namespace Microsoft.VisualStudio.Project
{

    /// <summary>
    /// All public properties on Nodeproperties or derived classes are assumed to be used by Automation by default.
    /// Set this attribute to false on Properties that should not be visible for Automation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class AutomationBrowsableAttribute : System.Attribute
    {
        public AutomationBrowsableAttribute(bool browsable)
        {
            this.browsable = browsable;
        }

        public bool Browsable
        {
            get
            {
                return this.browsable;
            }
        }

        private bool browsable;
    }

    /// <summary>
    /// To create your own localizable node properties, subclass this and add public properties
    /// decorated with your own localized display name, category and description attributes.
    /// </summary>
    [CLSCompliant(false), ComVisible(true)]
    public class NodeProperties : LocalizableProperties,
        ISpecifyPropertyPages,
        IVsGetCfgProvider,
        IVsSpecifyProjectDesignerPages,
        EnvDTE80.IInternalExtenderProvider,
        IVsBrowseObject
    {
        #region fields
        private HierarchyNode node;
        #endregion

        #region properties
        [Browsable(false)]
        [AutomationBrowsable(false)]
        public HierarchyNode Node
        {
            get { return this.node; }
        }

        /// <summary>
        /// Used by Property Pages Frame to set it's title bar. The Caption of the Hierarchy Node is returned.
        /// </summary>
        [Browsable(false)]
        [AutomationBrowsable(false)]
        public virtual string Name
        {
            get { return this.node.Caption; }
        }

        #endregion

        #region ctors
        public NodeProperties(HierarchyNode node)
        {
            if(node == null)
            {
                throw new ArgumentNullException("node");
            }
            this.node = node;
        }
        #endregion

        #region ISpecifyPropertyPages methods
        public virtual void GetPages(CAUUID[] pages)
        {
            this.GetCommonPropertyPages(pages);
        }
        #endregion

        #region IVsSpecifyProjectDesignerPages
        /// <summary>
        /// Implementation of the IVsSpecifyProjectDesignerPages. It will return the pages that are configuration independent.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        /// <returns></returns>
        public virtual int GetProjectDesignerPages(CAUUID[] pages)
        {
            this.GetCommonPropertyPages(pages);
            return VSConstants.S_OK;
        }
        #endregion

        #region IVsGetCfgProvider methods
        public virtual int GetCfgProvider(out IVsCfgProvider p)
        {
            p = null;
            return VSConstants.E_NOTIMPL;
        }
        #endregion

        #region IVsBrowseObject methods
        /// <summary>
        /// Maps back to the hierarchy or project item object corresponding to the browse object.
        /// </summary>
        /// <param name="hier">Reference to the hierarchy object.</param>
        /// <param name="itemid">Reference to the project item.</param>
        /// <returns>If the method succeeds, it returns S_OK. If it fails, it returns an error code. </returns>
        public virtual int GetProjectItem(out IVsHierarchy hier, out uint itemid)
        {
            if(this.node == null)
            {
                throw new InvalidOperationException();
            }
            hier = this.node.ProjectMgr.InteropSafeIVsHierarchy;
            itemid = this.node.ID;
            return VSConstants.S_OK;
        }
        #endregion

        #region overridden methods
        /// <summary>
        /// Get the Caption of the Hierarchy Node instance. If Caption is null or empty we delegate to base
        /// </summary>
        /// <returns>Caption of Hierarchy node instance</returns>
        public override string GetComponentName()
        {
            string caption = this.Node.Caption;
            if(string.IsNullOrEmpty(caption))
            {
                return base.GetComponentName();
            }
            else
            {
                return caption;
            }
        }
        #endregion

        #region helper methods
        protected string GetProperty(string name, string def)
        {
            string a = this.Node.ItemNode.GetMetadata(name);
            return (a == null) ? def : a;
        }

        protected void SetProperty(string name, string value)
        {
            this.Node.ItemNode.SetMetadata(name, value);
        }

        /// <summary>
        /// Retrieves the common property pages. The NodeProperties is the BrowseObject and that will be called to support 
        /// configuration independent properties.
        /// </summary>
        /// <param name="pages">The pages to return.</param>
        private void GetCommonPropertyPages(CAUUID[] pages)
        {
            // We do not check whether the supportsProjectDesigner is set to false on the ProjectNode.
            // We rely that the caller knows what to call on us.
            if(pages == null)
            {
                throw new ArgumentNullException("pages");
            }

            if(pages.Length == 0)
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "pages");
            }

            // Only the project should show the property page the rest should show the project properties.
            if(this.node != null && (this.node is ProjectNode))
            {
                // Retrieve the list of guids from hierarchy properties.
                // Because a flavor could modify that list we must make sure we are calling the outer most implementation of IVsHierarchy
                string guidsList = String.Empty;
                IVsHierarchy hierarchy = this.Node.ProjectMgr.InteropSafeIVsHierarchy;
                object variant = null;
                ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList, out variant));
                guidsList = (string)variant;

                Guid[] guids = Utilities.GuidsArrayFromSemicolonDelimitedStringOfGuids(guidsList);
                if(guids == null || guids.Length == 0)
                {
                    pages[0] = new CAUUID();
                    pages[0].cElems = 0;
                }
                else
                {
                    pages[0] = PackageUtilities.CreateCAUUIDFromGuidArray(guids);
                }
            }
            else
            {
                pages[0] = new CAUUID();
                pages[0].cElems = 0;
            }
        }
        #endregion

        #region IInternalExtenderProvider Members

        bool EnvDTE80.IInternalExtenderProvider.CanExtend(string extenderCATID, string extenderName, object extendeeObject)
        {
            EnvDTE80.IInternalExtenderProvider outerHierarchy = this.Node.ProjectMgr.InteropSafeIVsHierarchy as EnvDTE80.IInternalExtenderProvider;


            if(outerHierarchy != null)
            {
                return outerHierarchy.CanExtend(extenderCATID, extenderName, extendeeObject);
            }
            return false;
        }

        object EnvDTE80.IInternalExtenderProvider.GetExtender(string extenderCATID, string extenderName, object extendeeObject, EnvDTE.IExtenderSite extenderSite, int cookie)
        {
            EnvDTE80.IInternalExtenderProvider outerHierarchy = this.Node.ProjectMgr.InteropSafeIVsHierarchy as EnvDTE80.IInternalExtenderProvider;

            if(outerHierarchy != null)
            {
                return outerHierarchy.GetExtender(extenderCATID, extenderName, extendeeObject, extenderSite, cookie);
            }

            return null;
        }

        object EnvDTE80.IInternalExtenderProvider.GetExtenderNames(string extenderCATID, object extendeeObject)
        {
            EnvDTE80.IInternalExtenderProvider outerHierarchy = this.Node.ProjectMgr.InteropSafeIVsHierarchy as EnvDTE80.IInternalExtenderProvider;

            if(outerHierarchy != null)
            {
                return outerHierarchy.GetExtenderNames(extenderCATID, extendeeObject);
            }

            return null;
        }

        #endregion

        #region ExtenderSupport
        [Browsable(false)]
        [SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "CATID")]
        public virtual string ExtenderCATID
        {
            get
            {
                Guid catid = this.Node.ProjectMgr.GetCATIDForType(this.GetType());
                if(Guid.Empty.CompareTo(catid) == 0)
                {
                    return null;
                }
                return catid.ToString("B");
            }
        }

        [Browsable(false)]
        public object ExtenderNames()
        {
            EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)this.Node.GetService(typeof(EnvDTE.ObjectExtenders));
            Debug.Assert(extenderService != null, "Could not get the ObjectExtenders object from the services exposed by this property object");
            if(extenderService == null)
            {
                throw new InvalidOperationException();
            }
            return extenderService.GetExtenderNames(this.ExtenderCATID, this);
        }

        public object Extender(string extenderName)
        {
            EnvDTE.ObjectExtenders extenderService = (EnvDTE.ObjectExtenders)this.Node.GetService(typeof(EnvDTE.ObjectExtenders));
            Debug.Assert(extenderService != null, "Could not get the ObjectExtenders object from the services exposed by this property object");
            if(extenderService == null)
            {
                throw new InvalidOperationException();
            }
            return extenderService.GetExtender(this.ExtenderCATID, extenderName, this);
        }

        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class FileNodeProperties : NodeProperties
    {
        #region properties
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        public virtual BuildAction BuildAction
        {
            get
            {
                string value = this.Node.ItemNode.ItemName;
                if(value == null || value.Length == 0)
                {
                    return BuildAction.None;
                }
                return (BuildAction)Enum.Parse(typeof(BuildAction), value);
            }
            set
            {
                this.Node.ItemNode.ItemName = value.ToString();
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FileName)]
        [SRDescriptionAttribute(SR.FileNameDescription)]
        public string FileName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }

        #region non-browsable properties - used for automation only
        [Browsable(false)]
        public string Extension
        {
            get
            {
                return Path.GetExtension(this.Node.Caption);
            }
        }
        #endregion

        #endregion

        #region ctors
        public FileNodeProperties(HierarchyNode node)
            : base(node)
        {
        }
        #endregion

        #region overridden methods
        public override string GetClassName()
        {
            return SR.GetString(SR.FileProperties, CultureInfo.CurrentUICulture);
        }

        // !EFW - Added to return a read-only property descriptor for non-member items
        public override DesignPropertyDescriptor CreateDesignPropertyDescriptor(PropertyDescriptor propertyDescriptor)
        {
            if(this.Node.IsNonMemberItem)
                return new ReadOnlyDesignPropertyDescriptor(propertyDescriptor);

            return base.CreateDesignPropertyDescriptor(propertyDescriptor);
        }

        // !EFW - Added to exclude BuildAction for non-member items
        public override PropertyDescriptorCollection GetProperties()
        {
            var props = base.GetProperties();

            if(this.Node.IsNonMemberItem)
            {
                var p = props.Find("BuildAction", true);

                if(p != null)
                    props.Remove(p);
            }

            return props;
        }

        // !EFW - Added to exclude BuildAction for non-member items
        public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            var props = base.GetProperties(attributes);

            if(this.Node.IsNonMemberItem)
            {
                var p = props.Find("BuildAction", true);

                if(p != null)
                    props.Remove(p);
            }

            return props;
        }
        #endregion
    }

    // !EFW - Added for non-member file items
    /// <summary>
    /// This is a read-only property descriptor used for non-member file items
    /// </summary>
    public class ReadOnlyDesignPropertyDescriptor : DesignPropertyDescriptor
    {
        #region Constructor
        //=====================================================================

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlyDesignPropertyDescriptor"/> class.
        /// </summary>
        /// <param name="propertyDescriptor">The <see cref="PropertyDescriptor"/> to wrap.</param>
        public ReadOnlyDesignPropertyDescriptor(PropertyDescriptor propertyDescriptor) : base(propertyDescriptor)
        {
        }
        #endregion

        #region Overrides
        //=====================================================================

        /// <summary>
        /// Properties are always read-only for non-member items
        /// </summary>
        public override bool IsReadOnly
        {
            get { return true; }
        }

        /// <summary>
        /// This is overridden to show property values without bolding
        /// </summary>
        /// <param name="component">The component to check.</param>
        /// <returns>Always returns false</returns>
        public override bool ShouldSerializeValue(object component)
        {
            return false;
        }
        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class DependentFileNodeProperties : NodeProperties
    {
        #region properties
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.BuildAction)]
        [SRDescriptionAttribute(SR.BuildActionDescription)]
        public virtual BuildAction BuildAction
        {
            get
            {
                string value = this.Node.ItemNode.ItemName;
                if(value == null || value.Length == 0)
                {
                    return BuildAction.None;
                }
                return (BuildAction)Enum.Parse(typeof(BuildAction), value);
            }
            set
            {
                this.Node.ItemNode.ItemName = value.ToString();
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FileName)]
        [SRDescriptionAttribute(SR.FileNameDescription)]
        public virtual string FileName
        {
            get
            {
                return this.Node.Caption;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }
        #endregion

        #region ctors
        public DependentFileNodeProperties(HierarchyNode node)
            : base(node)
        {
        }

        #endregion

        #region overridden methods
        public override string GetClassName()
        {
            return SR.GetString(SR.FileProperties, CultureInfo.CurrentUICulture);
        }
        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class SingleFileGeneratorNodeProperties : FileNodeProperties
    {
        #region fields
        private EventHandler<HierarchyNodeEventArgs> onCustomToolChanged;
        private EventHandler<HierarchyNodeEventArgs> onCustomToolNameSpaceChanged;
        #endregion

        #region custom tool events
        internal event EventHandler<HierarchyNodeEventArgs> OnCustomToolChanged
        {
            add { onCustomToolChanged += value; }
            remove { onCustomToolChanged -= value; }
        }

        internal event EventHandler<HierarchyNodeEventArgs> OnCustomToolNameSpaceChanged
        {
            add { onCustomToolNameSpaceChanged += value; }
            remove { onCustomToolNameSpaceChanged -= value; }
        }

        #endregion

        #region properties
        [SRCategoryAttribute(SR.Advanced)]
        [LocDisplayName(SR.CustomTool)]
        [SRDescriptionAttribute(SR.CustomToolDescription)]
        public virtual string CustomTool
        {
            get
            {
                return this.Node.ItemNode.GetMetadata(ProjectFileConstants.Generator);
            }
            set
            {
                if (CustomTool != value)
                {
                    this.Node.ItemNode.SetMetadata(ProjectFileConstants.Generator, value != string.Empty ? value : null);
                    HierarchyNodeEventArgs args = new HierarchyNodeEventArgs(this.Node);
                    if (onCustomToolChanged != null)
                    {
                        onCustomToolChanged(this.Node, args);
                    }
                }
            }
        }

        [SRCategoryAttribute(VisualStudio.Project.SR.Advanced)]
        [LocDisplayName(SR.CustomToolNamespace)]
        [SRDescriptionAttribute(SR.CustomToolNamespaceDescription)]
        public virtual string CustomToolNamespace
        {
            get
            {
                return this.Node.ItemNode.GetMetadata(ProjectFileConstants.CustomToolNamespace);
            }
            set
            {
                if (CustomToolNamespace != value)
                {
                    this.Node.ItemNode.SetMetadata(ProjectFileConstants.CustomToolNamespace, value != String.Empty ? value : null);
                    HierarchyNodeEventArgs args = new HierarchyNodeEventArgs(this.Node);
                    if (onCustomToolNameSpaceChanged != null)
                    {
                        onCustomToolNameSpaceChanged(this.Node, args);
                    }
                }
            }
        }
        #endregion

        #region ctors
        public SingleFileGeneratorNodeProperties(HierarchyNode node)
            : base(node)
        {
        }
        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ProjectNodeProperties : NodeProperties
    {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFolder)]
        [SRDescriptionAttribute(SR.ProjectFolderDescription)]
        [AutomationBrowsable(false)]
        public string ProjectFolder
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFolder;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.ProjectFile)]
        [SRDescriptionAttribute(SR.ProjectFileDescription)]
        [AutomationBrowsable(false)]
        public string ProjectFile
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFile;
            }
            set
            {
                this.Node.ProjectMgr.ProjectFile = value;
            }
        }

        #region non-browsable properties - used for automation only
        [Browsable(false)]
        public string FileName
        {
            get
            {
                return this.Node.ProjectMgr.ProjectFile;
            }
            set
            {
                this.Node.ProjectMgr.ProjectFile = value;
            }
        }


        [Browsable(false)]
        public string FullPath
        {
            get
            {
                string fullPath = this.Node.ProjectMgr.ProjectFolder;
                if(!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    return fullPath + Path.DirectorySeparatorChar;
                }
                else
                {
                    return fullPath;
                }
            }
        }
        #endregion

        #endregion

        #region ctors
        public ProjectNodeProperties(ProjectNode node)
            : base(node)
        {
        }
        #endregion

        #region overridden methods
        public override string GetClassName()
        {
            return SR.GetString(SR.ProjectProperties, CultureInfo.CurrentUICulture);
        }

        /// <summary>
        /// ICustomTypeDescriptor.GetEditor
        /// To enable the "Property Pages" button on the properties browser
        /// the browse object (project properties) need to be unmanaged
        /// or it needs to provide an editor of type ComponentEditor.
        /// </summary>
        /// <param name="editorBaseType">Type of the editor</param>
        /// <returns>Editor</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", 
            Justification="The service provider is used by the PropertiesEditorLauncher")]
        public override object GetEditor(Type editorBaseType)
        {
            // Override the scenario where we are asked for a ComponentEditor
            // as this is how the Properties Browser calls us
            if(editorBaseType == typeof(ComponentEditor))
            {
                IOleServiceProvider sp;
                ErrorHandler.ThrowOnFailure(this.Node.GetSite(out sp));
                return new PropertiesEditorLauncher(new ServiceProvider(sp));
            }

            return base.GetEditor(editorBaseType);
        }

        public override int GetCfgProvider(out IVsCfgProvider p)
        {
            if(this.Node != null && this.Node.ProjectMgr != null)
            {
                return this.Node.ProjectMgr.GetCfgProvider(out p);
            }

            return base.GetCfgProvider(out p);
        }
        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class FolderNodeProperties : NodeProperties
    {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FolderName)]
        [SRDescriptionAttribute(SR.FolderNameDescription)]
        [AutomationBrowsable(false)]
        public string FolderName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
                this.Node.ReDraw(UIHierarchyElement.Caption);
            }
        }

        #region properties - used for automation only
        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FileName
        {
            get
            {
                return this.Node.Caption;
            }
            set
            {
                this.Node.SetEditLabel(value);
            }
        }

        [Browsable(false)]
        [AutomationBrowsable(true)]
        public string FullPath
        {
            get
            {
                string fullPath = this.Node.GetMkDocument();
                if(!fullPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    return fullPath + Path.DirectorySeparatorChar;
                }
                else
                {
                    return fullPath;
                }
            }
        }
        #endregion

        #endregion

        #region ctors
        public FolderNodeProperties(HierarchyNode node)
            : base(node)
        {
        }
        #endregion

        #region overridden methods
        public override string GetClassName()
        {
            return SR.GetString(SR.FolderProperties, CultureInfo.CurrentUICulture);
        }

        // !EFW - Added to return a read-only property descriptor for non-member items
        public override DesignPropertyDescriptor CreateDesignPropertyDescriptor(PropertyDescriptor propertyDescriptor)
        {
            if(this.Node.IsNonMemberItem)
                return new ReadOnlyDesignPropertyDescriptor(propertyDescriptor);

            return base.CreateDesignPropertyDescriptor(propertyDescriptor);
        }
        #endregion
    }

    [CLSCompliant(false), ComVisible(true)]
    public class ReferenceNodeProperties : NodeProperties
    {
        #region properties
        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.RefName)]
        [SRDescriptionAttribute(SR.RefNameDescription)]
        [Browsable(true)]
        [AutomationBrowsable(true)]
        public override string Name
        {
            get
            {
                return this.Node.Caption;
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.CopyToLocal)]
        [SRDescriptionAttribute(SR.CopyToLocalDescription)]
        public bool CopyToLocal
        {
            get
            {
                string copyLocal = this.GetProperty(ProjectFileConstants.Private, "False");
                if(copyLocal == null || copyLocal.Length == 0)
                    return true;
                return bool.Parse(copyLocal);
            }
            set
            {
                this.SetProperty(ProjectFileConstants.Private, value.ToString());
            }
        }

        [SRCategoryAttribute(SR.Misc)]
        [LocDisplayName(SR.FullPath)]
        [SRDescriptionAttribute(SR.FullPathDescription)]
        public virtual string FullPath
        {
            get
            {
                return this.Node.Url;
            }
        }
        #endregion

        #region ctors
        public ReferenceNodeProperties(HierarchyNode node)
            : base(node)
        {
        }
        #endregion

        #region overridden methods
        public override string GetClassName()
        {
            return SR.GetString(SR.ReferenceProperties, CultureInfo.CurrentUICulture);
        }
        #endregion
    }

    [ComVisible(true)]
    public class ProjectReferencesProperties : ReferenceNodeProperties
    {
        #region ctors
        public ProjectReferencesProperties(ProjectReferenceNode node)
            : base(node)
        {
        }
        #endregion

        #region overridden methods
        public override string FullPath
        {
            get
            {
                return ((ProjectReferenceNode)Node).ReferencedProjectOutputPath;
            }
        }
        #endregion

        // !EFW - Added support for ReferenceOutputAssembly
        [SRCategory(SR.Misc)]
        [LocDisplayName(SR.ReferenceOutputAssembly)]
        [SRDescription(SR.ReferenceOutputAssemblyDescription)]
        public virtual bool ReferenceOutputAssembly
        {
            get { return ((ProjectReferenceNode)this.Node).ReferenceOutputAssembly; }
            set { ((ProjectReferenceNode)this.Node).ReferenceOutputAssembly = value; }
        }
    }

    [ComVisible(true)]
    public class ComReferenceProperties : ReferenceNodeProperties
    {
        public ComReferenceProperties(ComReferenceNode node)
            : base(node)
        {
        }

        [SRCategory(SR.Misc)]
        [LocDisplayName(SR.EmbedInteropTypes)]
        [SRDescription(SR.EmbedInteropTypesDescription)]
        public virtual bool EmbedInteropTypes
        {
            get { return ((ComReferenceNode)this.Node).EmbedInteropTypes; }
            set { ((ComReferenceNode)this.Node).EmbedInteropTypes = value; }
        }
    }
}
