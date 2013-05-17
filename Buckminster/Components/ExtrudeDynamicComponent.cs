﻿using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Buckminster.Types;

namespace Buckminster.Components
{
    public class ExtrudeDynamicComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ExtrudeDynamicComponent()
            : base("Buckminster's Dynamic Extrude Component", "Extrude",
                "Gives thickness to mesh faces. Allows individual offsets to be specified for each vertex.",
                "Buckminster", "Modify")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new MeshParam(), "Mesh", "M", "Input mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Distance", "D", "Distance to extrude faces", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Symmetric", "S", "If true, distance is halved and projected either side of the parent mesh.", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new MeshParam(), "Mesh", "M", "Extruded mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            if (!DA.GetData(0, ref mesh)) { return; }

            List<double> distance = new List<double>();
            if (!DA.GetDataList<double>(1, distance)) { return; }

            bool sym = true;
            if (!DA.GetData(2, ref sym)) { return; }

            if (distance.Count != mesh.Vertices.Count)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Length of offset list does not match mesh vertex list.");
                return;
            }

            DA.SetData(0, mesh.Extrude(distance, sym));
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("{5af05fb3-2f56-430e-9779-7eba705c18de}"); }
        }
    }
}