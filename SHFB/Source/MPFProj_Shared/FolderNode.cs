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

//=============================================================================
// File    : FileNode.cs
// Updated : 04/18/2011
// Modifier: Eric Woodruff  (Eric@EWoodruff.us)
//
// This file has been modified to support "Show All Files" in the project.
// Search for "!EFW" to find the changes.  Note that these changes have not
// been tested with dependent files so expect bugs.
//
//    Date     Who  Comments
// ============================================================================
// 06/20/2008  EFW  Added support for "Show All Files"
// 03/20/2011  EFW  Updated to use MPFProj for VS2010
//=============================================================================

// Core Show All Files functionality implemented in FolderNode.cs:
// - Updated GetIconHandle() to show excluded folder icon.
// - Updated MenuCommandId to return an appropriate context menu for non-member items.
// - Implemented IncludeInProjectRecursive() for non-member item file nodes.

// Show All Files functionality not implemented in FolderNode.cs
// - Derived classes that override CreatePropertiesObject() should return a default
//   instance of FileNodeProperties rather than their custom properties if the node
//   is a non-member item.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;

// !EFW
using VsPkgMenus = Microsoft.VisualStudio.Project.VsMenus;

namespace Microsoft.VisualStudio.Project
{
    [CLSCompliant(false)]
    [ComVisible(true)]
    public class FolderNode : HierarchyNode
    {
        #region ctors
        /// <summary>
        /// Constructor for the FolderNode
        /// </summary>
        /// <param name="root">Root node of the hierarchy</param>
        /// <param name="relativePath">relative path from root i.e.: "NewFolder1\\NewFolder2\\NewFolder3</param>
        /// <param name="element">Associated project element</param>
        public FolderNode(ProjectNode root, string relativePath, ProjectElement element)
            : base(root, element)
        {
            if(relativePath == null)
            {
                throw new ArgumentNullException("relativePath");
            }

            this.VirtualNodeName = relativePath.TrimEnd('\\');
        }
        #endregion

        #region overridden properties
        public override int SortPriority
        {
            get { return DefaultSortOrderNode.FolderNode; }
        }

        /// <summary>
        /// This relates to the SCC glyph
        /// </summary>
        public override VsStateIcon StateIconIndex
        {
            get
            {
                // The SCC manager does not support being asked for the state icon of a folder (result of the operation is undefined)
                return VsStateIcon.STATEICON_NOSTATEICON;
            }
        }
        #endregion

        #region overridden methods
        protected override NodeProperties CreatePropertiesObject()
        {
            return new FolderNodeProperties(this);
        }

        protected internal override void DeleteFromStorage(string path)
        {
            this.DeleteFolder(path);
        }

        /// <summary>
        /// Get the automation object for the FolderNode
        /// </summary>
        /// <returns>An instance of the Automation.OAFolderNode type if succeeded</returns>
        public override object GetAutomationObject()
        {
            if(this.ProjectMgr == null || this.ProjectMgr.IsClosed)
            {
                return null;
            }

            return new Automation.OAFolderItem(this.ProjectMgr.GetAutomationObject() as Automation.OAProject, this);
        }

        public override object GetIconHandle(bool open)
        {
            // !EFW
            if(base.IsNonMemberItem)
                return this.ProjectMgr.ImageHandler.GetIconHandle(open ?
                    (int)ProjectNode.ImageName.OpenExcludedFolder : (int)ProjectNode.ImageName.ExcludedFolder);

            return this.ProjectMgr.ImageHandler.GetIconHandle(open ? (int)ProjectNode.ImageName.OpenFolder : (int)ProjectNode.ImageName.Folder);
        }

        /// <summary>
        /// Rename Folder
        /// </summary>
        /// <param name="label">new Name of Folder</param>
        /// <returns>VSConstants.S_OK, if succeeded</returns>
        public override int SetEditLabel(string label)
        {
            if(String.Compare(Path.GetFileName(this.Url.TrimEnd('\\')), label, StringComparison.Ordinal) == 0)
            {
                // Label matches current Name
                return VSConstants.S_OK;
            }

            string newPath = Path.Combine(new DirectoryInfo(this.Url).Parent.FullName, label);

            // Verify that No Directory/file already exists with the new name among current children
            for(HierarchyNode n = Parent.FirstChild; n != null; n = n.NextSibling)
            {
                if(n != this && String.Compare(n.Caption, label, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return ShowFileOrFolderAlreadExistsErrorMessage(newPath);
                }
            }

            // Verify that No Directory/file already exists with the new name on disk
            if(Directory.Exists(newPath) || File.Exists(newPath))
            {
                return ShowFileOrFolderAlreadExistsErrorMessage(newPath);
            }

            try
            {
                RenameFolder(label);

                //Refresh the properties in the properties window
                IVsUIShell shell = this.ProjectMgr.GetService(typeof(SVsUIShell)) as IVsUIShell;
                Debug.Assert(shell != null, "Could not get the ui shell from the project");
                ErrorHandler.ThrowOnFailure(shell.RefreshPropertyBrowser(0));

                // Notify the listeners that the name of this folder is changed. This will
                // also force a refresh of the SolutionExplorer's node.
                this.OnPropertyChanged(this, (int)__VSHPROPID.VSHPROPID_Caption, 0);
            }
            catch(Exception e)
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.RenameFolder, CultureInfo.CurrentUICulture), e.Message));
            }
            return VSConstants.S_OK;
        }

        /// <summary>
        /// Get the menu command ID for the file item
        /// </summary>
        public override int MenuCommandId
        {
            get
            {
                // !EFW
                if(base.IsNonMemberItem)
                    return VsPkgMenus.IDM_VS_CTXT_XPROJ_MULTIITEM;

                return VsMenus.IDM_VS_CTXT_FOLDERNODE;
            }
        }

        public override Guid ItemTypeGuid
        {
            get
            {
                return VSConstants.GUID_ItemType_PhysicalFolder;
            }
        }

        public override string Url
        {
            get
            {
                return Path.Combine(Path.GetDirectoryName(this.ProjectMgr.Url), this.VirtualNodeName) + "\\";
            }
        }

        public override string Caption
        {
            get
            {
                // it might have a backslash at the end... 
                // and it might consist of Grandparent\parent\this\
                string caption = this.VirtualNodeName;
                string[] parts;
                parts = caption.Split(Path.DirectorySeparatorChar);
                caption = parts[parts.GetUpperBound(0)];
                return caption;
            }
        }

        public override string GetMkDocument()
        {
            Debug.Assert(this.Url != null, "No url sepcified for this node");

            return this.Url;
        }

        /// <summary>
        /// Enumerate the files associated with this node.
        /// A folder node is not a file and as such no file to enumerate.
        /// </summary>
        /// <param name="files">The list of files to be placed under source control.</param>
        /// <param name="flags">The flags that are associated to the files.</param>
        protected internal override void GetSccFiles(System.Collections.Generic.IList<string> files, System.Collections.Generic.IList<tagVsSccFilesFlags> flags)
        {
            return;
        }

        /// <summary>
        /// This method should be overridden to provide the list of special files and associated flags for source control.
        /// </summary>
        /// <param name="sccFile">One of the file associated to the node.</param>
        /// <param name="files">The list of files to be placed under source control.</param>
        /// <param name="flags">The flags that are associated to the files.</param>
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Scc")]
        [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "scc")]
        protected internal override void GetSccSpecialFiles(string sccFile, IList<string> files, IList<tagVsSccFilesFlags> flags)
        {
            if(this.ExcludeNodeFromScc)
            {
                return;
            }

            if(files == null)
            {
                throw new ArgumentNullException("files");
            }

            if(flags == null)
            {
                throw new ArgumentNullException("flags");
            }

            if(string.IsNullOrEmpty(sccFile))
            {
                throw new ArgumentException(SR.GetString(SR.InvalidParameter, CultureInfo.CurrentUICulture), "sccFile");
            }

            // Get the file node for the file passed in.
            FileNode node = this.FindChild(sccFile) as FileNode;

            // Dependents do not participate directly in scc.
            if(node != null && !(node is DependentFileNode))
            {
                node.GetSccSpecialFiles(sccFile, files, flags);
            }
        }

        /// <summary>
        /// Recursevily walks the folder nodes and redraws the state icons
        /// </summary>
        protected internal override void UpdateSccStateIcons()
        {
            for(HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
            {
                child.UpdateSccStateIcons();
            }
        }

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result)
        {
            if(cmdGroup == VsMenus.guidStandardCommandSet97)
            {
                switch((VsCommands)cmd)
                {
                    case VsCommands.Copy:
                    case VsCommands.Paste:
                    case VsCommands.Cut:
                    case VsCommands.Rename:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands.NewFolder:
                    case VsCommands.AddNewItem:
                    case VsCommands.AddExistingItem:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else if(cmdGroup == VsMenus.guidStandardCommandSet2K)
            {
                // !EFW
                switch((VsCommands2K)cmd)
                {
                    case VsCommands2K.EXCLUDEFROMPROJECT:
                        if(!base.IsNonMemberItem)
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;

                    case VsCommands2K.INCLUDEINPROJECT:
                        if(base.IsNonMemberItem)
                            result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            else
            {
                return (int)OleConstants.OLECMDERR_E_UNKNOWNGROUP;
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation)
        {
            if(deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage)
            {
                return this.ProjectMgr.CanProjectDeleteItems;
            }
            return false;
        }

        // !EFW
        /// <summary>
        /// This method converts a non-member folder node into a member node including
        /// all of its child nodes.
        /// </summary>
        /// <returns>Returns S_OK on success or an error code on failure</returns>
        protected internal int IncludeInProjectRecursive()
        {
            int result = this.IncludeInProject();

            if(result == VSConstants.S_OK)
                for(HierarchyNode node = this.FirstChild; node != null; node = node.NextSibling)
                {
                    FolderNode fn = node as FolderNode;

                    if(fn != null)
                        result = fn.IncludeInProjectRecursive();
                    else
                        result = node.IncludeInProject();

                    if(result != VSConstants.S_OK)
                        break;
                }

            return result;
        }
        #endregion

        #region virtual methods
        /// <summary>
        /// Override if your node is not a file system folder so that
        /// it does nothing or it deletes it from your storage location.
        /// </summary>
        /// <param name="path">Path to the folder to delete</param>
        public virtual void DeleteFolder(string path)
        {
            if(Directory.Exists(path))
                Directory.Delete(path, true);
        }

        /// <summary>
        /// creates the physical directory for a folder node
        /// Override if your node does not use file system folder
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "e")]
        public virtual void CreateDirectory()
        {
            try
            {
                if(Directory.Exists(this.Url) == false)
                {
                    Directory.CreateDirectory(this.Url);
                }
            }
            //TODO - this should not digest all exceptions.
            catch(System.Exception e)
            {
                CCITracing.Trace(e);
                throw;
            }
        }
        /// <summary>
        /// Creates a folder nodes physical directory
        /// Override if your node does not use file system folder
        /// </summary>
        /// <param name="newName"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "e")]
        public virtual void CreateDirectory(string newName)
        {
            if(String.IsNullOrEmpty(newName))
            {
                throw new ArgumentException(SR.GetString(SR.ParameterCannotBeNullOrEmpty, CultureInfo.CurrentUICulture), "newName");
            }

            try
            {
                // on a new dir && enter, we get called with the same name (so do nothing if name is the same
                char[] dummy = new char[1];
                dummy[0] = Path.DirectorySeparatorChar;
                string oldDir = this.Url;
                oldDir = oldDir.TrimEnd(dummy);
                string strNewDir = Path.Combine(Path.GetDirectoryName(oldDir), newName);

                if(String.Compare(strNewDir, oldDir, StringComparison.OrdinalIgnoreCase) != 0)
                {
                    if(Directory.Exists(strNewDir))
                    {
                        throw new InvalidOperationException(SR.GetString(SR.DirectoryExistError, CultureInfo.CurrentUICulture));
                    }
                    Directory.CreateDirectory(strNewDir);
                }
            }
            //TODO - this should not digest all exceptions.
            catch(System.Exception e)
            {
                CCITracing.Trace(e);
                throw;
            }
        }

        /// <summary>
        /// Rename the physical directory for a folder node
        /// Override if your node does not use file system folder
        /// </summary>
        /// <returns></returns>
        public virtual void RenameDirectory(string newPath)
        {
            if(Directory.Exists(this.Url))
            {
                if(Directory.Exists(newPath))
                {
                    ShowFileOrFolderAlreadExistsErrorMessage(newPath);
                }

                Directory.Move(this.Url, newPath);
            }
        }
        #endregion

        #region helper methods
        private void RenameFolder(string newName)
        {
            // Do the rename (note that we only do the physical rename if the leaf name changed)
            string newPath = Path.Combine(this.Parent.VirtualNodeName, newName);
            if(String.Compare(Path.GetFileName(VirtualNodeName), newName, StringComparison.Ordinal) != 0)
            {
                this.RenameDirectory(Path.Combine(this.ProjectMgr.ProjectFolder, newPath));
            }
            this.VirtualNodeName = newPath;

            this.ItemNode.Rename(VirtualNodeName);

            // Let all children know of the new path
            for(HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling)
            {
                FolderNode node = child as FolderNode;

                if(node == null)
                {
                    child.SetEditLabel(child.Caption);
                }
                else
                {
                    node.RenameFolder(node.Caption);
                }
            }

            // Some of the previous operation may have changed the selection so set it back to us
            IVsUIHierarchyWindow uiWindow = UIHierarchyUtilities.GetUIHierarchyWindow(this.ProjectMgr.Site, SolutionExplorer);
            // This happens in the context of renaming a folder.
            // Since we are already in solution explorer, it is extremely unlikely that we get a null return.
            // If we do, the consequences are minimal: the parent node will be selected instead of the
            // renamed node.
            if (uiWindow != null)
            {
                ErrorHandler.ThrowOnFailure(uiWindow.ExpandItem(this.ProjectMgr.InteropSafeIVsUIHierarchy, this.ID, EXPANDFLAGS.EXPF_SelectItem));
            }
        }

        /// <summary>
        /// Show error message if not in automation mode, otherwise throw exception
        /// </summary>
        /// <param name="newPath">path of file or folder already existing on disk</param>
        /// <returns>S_OK</returns>
        private int ShowFileOrFolderAlreadExistsErrorMessage(string newPath)
        {
            //A file or folder with the name '{0}' already exists on disk at this location. Please choose another name.
            //If this file or folder does not appear in the Solution Explorer, then it is not currently part of your project. To view files which exist on disk, but are not in the project, select Show All Files from the Project menu.
            string errorMessage = (String.Format(CultureInfo.CurrentCulture, SR.GetString(SR.FileOrFolderAlreadyExists, CultureInfo.CurrentUICulture), newPath));
            if(!Utilities.IsInAutomationFunction(this.ProjectMgr.Site))
            {
                string title = null;
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, errorMessage, icon, buttons, defaultButton);
                return VSConstants.S_OK;
            }
            else
            {
                throw new InvalidOperationException(errorMessage);
            }
        }

        #endregion
    }
}
