﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

using Grasshopper.Kernel;
using Rhino.Geometry;
using Solver = Google.OrTools.LinearSolver.Solver;

using Buckminster.Types;
using Mesh = Buckminster.Types.Mesh;
//using Molecular = SharpSLO.Types.Molecular;

namespace Buckminster.Components
{
    public class TopOptComponent : GH_Component
    {
        private enum Mode
        {
            None,
            FullyConnected,
            MemberAdding
        }
        private Mode m_mode;
        private Molecular m_world;
        private List<string> m_output;
        private bool m_mosek;

        /// <summary>
        /// Initializes a new instance of the TopOptComponent class.
        /// </summary>
        public TopOptComponent()
            : base("Buckminster's Topology Optimisation", "TopOpt",
                "Sheffield layout optimisation.",
                "Buckminster", "Analysis")
        {
            m_mode = Mode.None;
            m_output = new List<string>();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddParameter(new MolecularParam(), "Molecular", "Molecular", "Input structure. (Accepts a Rhino mesh, a PolyMesh or a native Molecular data type.)", GH_ParamAccess.item);
            pManager.AddParameter(new MolecularParam(), "Potentials", "PCL", "Potential connections list for member-additive ", GH_ParamAccess.item);
            pManager[1].Optional = true;
            pManager.AddVectorParameter("Fixities", "Fixities", "Nodal support conditions, represented as a vector (0: fixed, 1: free)", GH_ParamAccess.list);
            pManager.AddVectorParameter("Forces", "Forces", "Nodal load conditions, represented as a vector", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tensile", "-Limit", "Tensile capacity", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Compressive", "+Limit", "Compressive capacity", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Joint cost", "Joint", "Joint cost", GH_ParamAccess.item, 0.0);
            pManager.AddBooleanParameter("Reset", "Reset", "Reset", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Output", "Output", "Output", GH_ParamAccess.list);
            pManager.AddNumberParameter("Volume", "Volume", "Volume", GH_ParamAccess.item);
            pManager.AddLineParameter("Bars", "Bars", "Bars", GH_ParamAccess.list);
            pManager.AddNumberParameter("Radii", "Radii", "Radii", GH_ParamAccess.list);
            pManager.AddColourParameter("Colours", "Colours", "Colours", GH_ParamAccess.list);
            pManager.AddVectorParameter("Displacements", "Displacements", "Displacements", GH_ParamAccess.list);
        }

        /// <summary>
        /// Called before SolveInstance. (Equivalent to DA.Iteration == 0.)
        /// </summary>
        protected override void BeforeSolveInstance()
        {
            base.BeforeSolveInstance();
            this.ValuesChanged();
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Collect inputs
            SharpSLO.Types.Molecular molecular = null;
            SharpSLO.Types.Molecular pcl1 = null;
            List<Vector3d> fixities = new List<Vector3d>();
            List<Vector3d> forces = new List<Vector3d>();
            double limitT, limitC, jCost;
            limitT = limitC = jCost = double.NaN;
            bool reset = true;

            if (!DA.GetData(0, ref molecular)) return;
            DA.GetData(1, ref pcl1); // Optional
            if (!DA.GetDataList<Vector3d>(2, fixities)) return;
            if (!DA.GetDataList<Vector3d>(3, forces)) return;
            if (!DA.GetData<double>(4, ref limitT)) return;
            if (!DA.GetData<double>(5, ref limitC)) return;
            if (!DA.GetData<double>(6, ref jCost)) return;
            if (!DA.GetData<bool>(7, ref reset)) return;

            if (reset) // Rebuild model from external source
            {
                //m_world = molecular.Duplicate(); // copy molecular
                ///////////////////////////////////////////////////////
                m_world = new Molecular(molecular.Nodes.Count);
                foreach (var node in molecular.Nodes)
                {
                    m_world.NewVertex(node.X, node.Y, node.Z);
                }
                foreach (var bar in molecular.Bars)
                {
                    m_world.NewEdge(m_world.listVertexes[bar.Start], m_world.listVertexes[bar.End]);
                }

                Molecular pcl = null;
                if (pcl1 != null)
                {
                    pcl = new Molecular(pcl1.Nodes.Count);
                    foreach (var node in pcl1.Nodes)
                    {
                        m_world.NewVertex(node.X, node.Y, node.Z);
                    }
                    foreach (var bar in pcl1.Bars)
                    {
                        m_world.NewEdge(pcl.listVertexes[bar.Start], pcl.listVertexes[bar.End]);
                    }
                }
                ///////////////////////////////////////////////////////

                // Add boundary conditions
                for (int i = 0; i < m_world.listVertexes.Count; i++)
                {
                    m_world.listVertexes[i].Fixity = new Molecular.Constraint(fixities[i]);
                    m_world.listVertexes[i].Force = new Vector3d(forces[i]);
                }

                if (m_mode == Mode.FullyConnected) // discard mesh edges and used a fully-connected ground-structure
                {
                    // clear existing edges from molecular structure
                    m_world.DeleteElements(m_world.listEdges.ToArray()); // copy list
                    // add edges to create fully-connected ground-structure
                    for (int i = 0; i < m_world.listVertexes.Count; i++)
                        for (int j = i + 1; j < m_world.listVertexes.Count; j++)
                            m_world.NewEdge(m_world.listVertexes[i], m_world.listVertexes[j]);
                }

                TopOpt.SetProblem(m_world, pcl, limitT, limitC, jCost); // set up TopOpt parameters

                if (m_output.Count > 0) m_output.Clear();
            }
            
            // solve
            string msg;
            bool success;
            if (m_mosek)
                success = TopOpt.SolveProblemMosek(out msg);
            else
                success = TopOpt.SolveProblemGoogle(out msg);
            if (success)
            {
                if (m_mode == Mode.MemberAdding) TopOpt.AddEdges(0.1, 0);
                else TopOpt.MembersAdded = 0; // Reset no. members added to avoid confusion

                if (TopOpt.MembersAdded == 0) StopTimer(); // Disable timer if solution converges

                m_output.Add(string.Format("{0,3:D}: vol.: {1,9:F6} add. :{2,4:D} ({3,2:F3}s)", m_output.Count, TopOpt.Volume, TopOpt.MembersAdded, TopOpt.RunTime));

                // set outputs
                DA.SetDataList(0, m_output);
                DA.SetData(1, TopOpt.Volume);
                var subset = m_world.listEdges.Where(e => e.Radius > 1E-6); // Filter out unstressed bars
                DA.SetDataList(2, subset.Select(e => new Line(e.StartVertex.Coord, e.EndVertex.Coord)));
                DA.SetDataList(3, subset.Select(e => e.Radius));
                DA.SetDataList(4, subset.Select(e => e.Colour));
                DA.SetDataList(5, m_world.listVertexes.Select(v => v.Velocity));
            }
            else
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, msg);
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
            get { return new Guid("{2622766f-eb60-4a9c-ba82-7ce226866ec7}"); }
        }

        public override void DrawViewportWires(IGH_PreviewArgs args)
        {
            if (Hidden) return;
            if (Locked) return;
            if (m_world == null) return;
            foreach (var edge in m_world.listEdges)
            {
                if (edge.Radius > 1E-6) // don't draw unstressed
                {
                    System.Drawing.Color colour = this.Attributes.Selected ? args.WireColour_Selected : edge.Colour;
                    var thickness = (int)Math.Floor(edge.Radius * 5) + 1;
                    if (thickness < 1) thickness = 1; // Ensure stressed elements are visible (1px minimum)
                    args.Display.DrawLine(edge.StartVertex.Coord, edge.EndVertex.Coord, colour, thickness);
                }
            }
        }

        public override void AppendAdditionalMenuItems(System.Windows.Forms.ToolStripDropDown menu)
        {
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem1 = Menu_AppendItem(menu, "Fully-Connected", new EventHandler(this.Menu_FullyConnectedClicked), true, m_mode == Mode.FullyConnected);
            ToolStripMenuItem toolStripMenuItem2 = Menu_AppendItem(menu, "Member-Adding", new EventHandler(this.Menu_MemberAddingClicked), true, m_mode == Mode.MemberAdding);
            toolStripMenuItem1.ToolTipText = "Discard mesh-edges and use a fully-connected ground-structure (slow).";
            toolStripMenuItem2.ToolTipText = "Use the member-adding algorithm (fast).";
            Menu_AppendSeparator(menu);
            ToolStripMenuItem toolStripMenuItem3 = Menu_AppendItem(menu, "Mosek", new EventHandler(this.Menu_MosekClicked), true, m_mosek);
            toolStripMenuItem3.ToolTipText = "Use the Mosek solver (if you have it installed).";
        }

        private void Menu_FullyConnectedClicked(Object sender, EventArgs e)
        {
            RecordUndoEvent("FullyConnected");
            if (m_mode == Mode.FullyConnected)
                m_mode = Mode.None;
            else
                m_mode = Mode.FullyConnected;
            ExpireSolution(true);
        }

        private void Menu_MemberAddingClicked(Object sender, EventArgs e)
        {
            RecordUndoEvent("MemberAdding");
            if (m_mode == Mode.MemberAdding)
                m_mode = Mode.None;
            else
                m_mode = Mode.MemberAdding;
            ExpireSolution(true);
        }

        private void Menu_MosekClicked(Object sender, EventArgs e)
        {
            RecordUndoEvent("Mosek");
            m_mosek = m_mosek ? false : true;
            ExpireSolution(true);
        }

        protected override void ValuesChanged()
        {
            switch (m_mode)
            {
                case Mode.None:
                    this.Message = null;
                    break;
                case Mode.FullyConnected:
                    this.Message = "Fully-Connected";
                    break;
                case Mode.MemberAdding:
                    this.Message = "Member-Adding";
                    break;
            }
        }

        private bool StopTimer()
        {
            // http://www.grasshopper3d.com/forum/topics/how-to-stop-the-timer-component-in-the-vb-script
            // We need to disable the timer that is associated with this component.
            // First, find the document that contains this component
            GH_Document ghdoc = OnPingDocument();
            if (ghdoc == null) return false;
            // Then, iterate over all objects in the document to find all timers.
            foreach (IGH_DocumentObject docobj in ghdoc.Objects)
            {
                // Try to cast the object to a GH_Timer
                Grasshopper.Kernel.Special.GH_Timer timer = docobj as Grasshopper.Kernel.Special.GH_Timer;
                if (timer == null) continue;
                // If the cast was successful, then see if this component is part of the timer target list.
                if (timer.Targets.Contains(InstanceGuid))
                {
                    // If it is, lock the timer.
                    timer.Locked = true;
                    return timer.Locked;
                }
            }
            return false; // Didn't find a timer attached to this component...
        }
    }
}