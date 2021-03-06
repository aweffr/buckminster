﻿using System;

using Grasshopper.Kernel;
using Buckminster.Types;

namespace Buckminster.Components
{
    public class FlipComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the OffsetComponent class.
        /// </summary>
        public FlipComponent()
            : base("Buckminster's Flip Mesh Faces", "Flip",
                "Flips a meshes faces, reversing the surface normal direction",
                "Buckminster", "Modify")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new MeshParam(), "Mesh", "M", "Input mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MeshParam(), "Mesh", "M", "Flipped mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh)) { return; }

            Mesh target = mesh.Duplicate();
            target.Halfedges.Flip();

            DA.SetData(0, target);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{35690e63-471e-4ebb-b347-69caa5251a6f}"); }
        }
    }
}