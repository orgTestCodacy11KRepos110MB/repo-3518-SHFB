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
// File    : SolutionListenerForProjectReferenceUpdate.cs
// Updated : 05/10/2013
// Modifier: Eric Woodruff  (Eric@EWoodruff.us)
//
//    Date     Who  Comments
// ==============================================================================================================
// 05/10/2013  EFW  Added code to ignore closed files not hosted within a project in the solution
//===============================================================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudio.Project
{
    [CLSCompliant(false)]
    public class SolutionListenerForProjectReferenceUpdate : SolutionListener
    {
        #region ctor
        public SolutionListenerForProjectReferenceUpdate(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }
        #endregion

        #region overridden methods
        /// <summary>
        /// Delete this project from the references of projects of this type, if it is found.
        /// </summary>
        /// <param name="hierarchy"></param>
        /// <param name="removed"></param>
        /// <returns></returns>
        public override int OnBeforeCloseProject(IVsHierarchy hierarchy, int removed)
        {
            if(removed != 0)
            {
                List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(hierarchy);

                foreach(ProjectReferenceNode projectReference in projectReferences)
                {
                    projectReference.Remove(false);
                    // Set back the remove state on the project refererence. The reason why we are doing this is that the OnBeforeUnloadProject immedaitely calls
                    // OnBeforeCloseProject, thus we would be deleting references when we should not. Unload should not remove references.
                    projectReference.CanRemoveReference = true;
                }
            }

            return VSConstants.S_OK;
        }


        /// <summary>
        /// Needs to update the dangling reference on projects that contain this hierarchy as project reference.
        /// </summary>
        /// <param name="stubHierarchy"></param>
        /// <param name="realHierarchy"></param>
        /// <returns></returns>
        public override int OnAfterLoadProject(IVsHierarchy stubHierarchy, IVsHierarchy realHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(realHierarchy);

            // Refersh the project reference node. That should trigger the drawing of the normal project reference icon.
            foreach(ProjectReferenceNode projectReference in projectReferences)
            {
                projectReference.CanRemoveReference = true;

                projectReference.OnInvalidateItems(projectReference.Parent);
            }

            return VSConstants.S_OK;
        }


        public override int OnAfterRenameProject(IVsHierarchy hierarchy)
        {
            if(hierarchy == null)
            {
                return VSConstants.E_INVALIDARG;
            }

            try
            {
                List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(hierarchy);

                // Collect data that is needed to initialize the new project reference node.
                string projectRef;
                ErrorHandler.ThrowOnFailure(this.Solution.GetProjrefOfProject(hierarchy, out projectRef));

                object nameAsObject;
                ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_Name, out nameAsObject));
                string projectName = (string)nameAsObject;

                string projectPath = String.Empty;

                IVsProject3 project = hierarchy as IVsProject3;

                if(project != null)
                {
                    ErrorHandler.ThrowOnFailure(project.GetMkDocument(VSConstants.VSITEMID_ROOT, out projectPath));
                    projectPath = Path.GetDirectoryName(projectPath);
                }

                // Remove and re add the node.
                foreach(ProjectReferenceNode projectReference in projectReferences)
                {
                    ProjectNode projectMgr = projectReference.ProjectMgr;
                    IReferenceContainer refContainer = projectMgr.GetReferenceContainer();
                    projectReference.Remove(false);

                    VSCOMPONENTSELECTORDATA selectorData = new VSCOMPONENTSELECTORDATA();
                    selectorData.type = VSCOMPONENTTYPE.VSCOMPONENTTYPE_Project;
                    selectorData.bstrTitle = projectName;
                    selectorData.bstrFile = projectPath;
                    selectorData.bstrProjRef = projectRef;
                    refContainer.AddReferenceFromSelectorData(selectorData);
                }
            }
            catch(COMException e)
            {
                return e.ErrorCode;
            }

            return VSConstants.S_OK;
        }


        public override int OnBeforeUnloadProject(IVsHierarchy realHierarchy, IVsHierarchy stubHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = this.GetProjectReferencesContainingThisProject(realHierarchy);

            // Refresh the project reference node. That should trigger the drawing of the dangling project reference icon.
            foreach(ProjectReferenceNode projectReference in projectReferences)
            {
                projectReference.IsNodeValid = true;
                projectReference.OnInvalidateItems(projectReference.Parent);
                projectReference.CanRemoveReference = false;
                projectReference.IsNodeValid = false;
                projectReference.ReferencedProjectObject = null;
            }

            return VSConstants.S_OK;

        }

        #endregion

        #region helper methods
        private List<ProjectReferenceNode> GetProjectReferencesContainingThisProject(IVsHierarchy inputHierarchy)
        {
            List<ProjectReferenceNode> projectReferences = new List<ProjectReferenceNode>();
            if(this.Solution == null || inputHierarchy == null)
            {
                return projectReferences;
            }

            uint flags = (uint)(__VSENUMPROJFLAGS.EPF_ALLPROJECTS | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION);
            Guid enumOnlyThisType = Guid.Empty;
            IEnumHierarchies enumHierarchies = null;

            ErrorHandler.ThrowOnFailure(this.Solution.GetProjectEnum(flags, ref enumOnlyThisType, out enumHierarchies));
            Debug.Assert(enumHierarchies != null, "Could not get list of hierarchies in solution");

            IVsHierarchy[] hierarchies = new IVsHierarchy[1];
            uint fetched;
            int returnValue = VSConstants.S_OK;
            do
            {
                returnValue = enumHierarchies.Next(1, hierarchies, out fetched);
                Debug.Assert(fetched <= 1, "We asked one project to be fetched VSCore gave more than one. We cannot handle that");
                if(returnValue == VSConstants.S_OK && fetched == 1)
                {
                    IVsHierarchy hierarchy = hierarchies[0];
                    Debug.Assert(hierarchy != null, "Could not retrieve a hierarchy");
                    IReferenceContainerProvider provider = hierarchy as IReferenceContainerProvider;
                    if(provider != null)
                    {
                        IReferenceContainer referenceContainer = provider.GetReferenceContainer();

                        Debug.Assert(referenceContainer != null, "Could not found the References virtual node");
                        ProjectReferenceNode projectReferenceNode = GetProjectReferenceOnNodeForHierarchy(referenceContainer.EnumReferences(), inputHierarchy);
                        if(projectReferenceNode != null)
                        {
                            projectReferences.Add(projectReferenceNode);
                        }
                    }
                }
            } while(returnValue == VSConstants.S_OK && fetched == 1);

            return projectReferences;
        }

        private static ProjectReferenceNode GetProjectReferenceOnNodeForHierarchy(IList<ReferenceNode> references, IVsHierarchy inputHierarchy)
        {
            if(references == null)
                return null;

            Guid projectGuid;

            // !EFW - This is an odd one.  Open a file not contained within a project in the active solution and
            // then close it.  When you do, this method is called and it fails here.  The file is hosted in the
            // Miscellaneous Files hierarchy.  In such cases, we'll just ignore it and return null.
            if(inputHierarchy.GetGuidProperty(VSConstants.VSITEMID_ROOT,
              (int)__VSHPROPID.VSHPROPID_ProjectIDGuid, out projectGuid) != VSConstants.S_OK)
                return null;

            string canonicalName;
            ErrorHandler.ThrowOnFailure(inputHierarchy.GetCanonicalName(VSConstants.VSITEMID_ROOT, out canonicalName));

            foreach(ReferenceNode refNode in references)
            {
                ProjectReferenceNode projRefNode = refNode as ProjectReferenceNode;

                if(projRefNode != null)
                {
                    if(projRefNode.ReferencedProjectGuid == projectGuid)
                        return projRefNode;

                    // Try with canonical names, if the project that is removed is an unloaded project that the
                    // above criteria will not pass.
                    if(!String.IsNullOrEmpty(projRefNode.Url) && NativeMethods.IsSamePath(projRefNode.Url, canonicalName))
                        return projRefNode;
                }
            }

            return null;
        }
        #endregion
    }
}
