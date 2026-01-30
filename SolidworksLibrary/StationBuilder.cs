using System;
using System.Collections.Generic;
using CAD;
using Mathematics;
using SldWorks;
using SwConst;

namespace SolidworksLibrary
{
    public class StationBuilder
    {
        public enum CoordinateSystemType
        {
            Cartesian,
            Cylindrical,
            Spherical
        }

        public enum CartesianAxis { X, Y, Z }
        public enum CylindricalAxis { R, Theta, Z }
        public enum SphericalAxis { R, Theta, Phi }

        public SolidworksModel MyModel { get; set; }
        public SldWorks.SldWorks SwApp { get; set; }
        public int Quantity { get; set; }
        public double Spacing { get; set; }
        public CoordinateSystemType CoordSystem { get; set; }
        public int Axis { get; set; }

        public List<CAD_Station> Stations { get; private set; }

        public StationBuilder(SldWorks.SldWorks swApp, SolidworksModel model, int quantity, double spacing,
            CoordinateSystemType coordSystem, int axis)
        {
            SwApp = swApp;
            MyModel = model;
            Quantity = quantity;
            Spacing = spacing;
            CoordSystem = coordSystem;
            Axis = axis;
            Stations = new List<CAD_Station>();
        }

        public void Build()
        {
            ModelDoc2 swModel = (ModelDoc2)MyModel.SwModelObject;
            FeatureManager featMgr = swModel.FeatureManager;

            for (int i = 0; i < Quantity; i++)
            {
                double offset = i * Spacing;
                double[] position = CalculatePosition(offset);

                CAD_SketchPlane sketchPlane = CreateOffsetPlane(swModel, featMgr, position, i);
                if (sketchPlane != null)
                {
                    CreateCoordinateSystem(swModel, featMgr, position, i, sketchPlane);
                    CreateSketchOnPlane(swModel, sketchPlane, i);

                    CAD_Station station = new CAD_Station(sketchPlane,
                        "Station_" + (i + 1), GetStationType());
                    station.Name = "Station_" + (i + 1);
                    station.SetLocation(station.MyType, offset);
                    station.MyModel = MyModel.MyCADModel;

                    Stations.Add(station);
                }
            }
        }

        public CAD_Station.StationTypeEnum GetStationType()
        {
            switch (CoordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    return CAD_Station.StationTypeEnum.Axial;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)Axis;
                    if (cyAxis == CylindricalAxis.Theta)
                        return CAD_Station.StationTypeEnum.Angular;
                    if (cyAxis == CylindricalAxis.R)
                        return CAD_Station.StationTypeEnum.Radial;
                    return CAD_Station.StationTypeEnum.Axial;
                case CoordinateSystemType.Spherical:
                    SphericalAxis spAxis = (SphericalAxis)Axis;
                    if (spAxis == SphericalAxis.R)
                        return CAD_Station.StationTypeEnum.Radial;
                    return CAD_Station.StationTypeEnum.Angular;
                default:
                    return CAD_Station.StationTypeEnum.Other;
            }
        }

        public CAD_SketchPlane.GeometryTypeEnum GetGeometryType()
        {
            switch (CoordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    return CAD_SketchPlane.GeometryTypeEnum.Cartesian;
                case CoordinateSystemType.Cylindrical:
                    return CAD_SketchPlane.GeometryTypeEnum.Cylindrical;
                case CoordinateSystemType.Spherical:
                    return CAD_SketchPlane.GeometryTypeEnum.Spherical;
                default:
                    return CAD_SketchPlane.GeometryTypeEnum.Cartesian;
            }
        }

        public double[] CalculatePosition(double offset)
        {
            double[] position = new double[3];

            switch (CoordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    position = CalculateCartesian(offset);
                    break;
                case CoordinateSystemType.Cylindrical:
                    position = CalculateCylindrical(offset);
                    break;
                case CoordinateSystemType.Spherical:
                    position = CalculateSpherical(offset);
                    break;
            }

            return position;
        }

        private double[] CalculateCartesian(double offset)
        {
            double[] position = new double[3];
            CartesianAxis axis = (CartesianAxis)Axis;

            switch (axis)
            {
                case CartesianAxis.X:
                    position[0] = offset;
                    break;
                case CartesianAxis.Y:
                    position[1] = offset;
                    break;
                case CartesianAxis.Z:
                    position[2] = offset;
                    break;
            }

            return position;
        }

        private double[] CalculateCylindrical(double offset)
        {
            double[] position = new double[3];
            CylindricalAxis axis = (CylindricalAxis)Axis;

            switch (axis)
            {
                case CylindricalAxis.R:
                    position[0] = offset;
                    break;
                case CylindricalAxis.Theta:
                    double theta = offset;
                    position[0] = Spacing * Math.Cos(theta);
                    position[1] = Spacing * Math.Sin(theta);
                    break;
                case CylindricalAxis.Z:
                    position[2] = offset;
                    break;
            }

            return position;
        }

        private double[] CalculateSpherical(double offset)
        {
            double[] position = new double[3];
            SphericalAxis axis = (SphericalAxis)Axis;

            switch (axis)
            {
                case SphericalAxis.R:
                    position[0] = offset;
                    break;
                case SphericalAxis.Theta:
                    double theta = offset;
                    position[0] = Spacing * Math.Sin(theta);
                    position[2] = Spacing * Math.Cos(theta);
                    break;
                case SphericalAxis.Phi:
                    double phi = offset;
                    position[0] = Spacing * Math.Cos(phi);
                    position[1] = Spacing * Math.Sin(phi);
                    break;
            }

            return position;
        }

        private CAD_SketchPlane CreateOffsetPlane(ModelDoc2 swModel, FeatureManager featMgr,
            double[] position, int index)
        {
            double distance = Math.Sqrt(position[0] * position[0] +
                                        position[1] * position[1] +
                                        position[2] * position[2]);

            string planeName = "Station_Plane_" + (index + 1);
            double[] normal = GetPlaneNormal();

            if (distance == 0 && index == 0)
            {
                Feature baseFeat = GetBasePlane(swModel);
                if (baseFeat == null) return null;

                CAD_SketchPlane sketchPlane = new CAD_SketchPlane(planeName,
                    CAD_SketchPlane.FunctionalTypeEnum.Section, GetGeometryType());
                sketchPlane.SetNormal(normal[0], normal[1], normal[2]);
                sketchPlane.MyModel = MyModel.MyCADModel;
                sketchPlane.Path = baseFeat.Name;
                return sketchPlane;
            }

            Feature baseFeature = GetBasePlane(swModel);
            if (baseFeature == null) return null;

            baseFeature.Select2(false, 0);
            RefPlane refPlane = (RefPlane)featMgr.InsertRefPlane(
                (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance,
                distance, 0, 0, 0, 0);

            if (refPlane != null)
            {
                Feature feat = (Feature)refPlane;
                feat.Name = planeName;
            }

            CAD_SketchPlane result = new CAD_SketchPlane(planeName,
                CAD_SketchPlane.FunctionalTypeEnum.Section, GetGeometryType());
            result.SetNormal(normal[0], normal[1], normal[2]);
            result.MyModel = MyModel.MyCADModel;
            result.Path = planeName;
            return result;
        }

        private double[] GetPlaneNormal()
        {
            double[] normal = new double[3];

            switch (CoordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    CartesianAxis cAxis = (CartesianAxis)Axis;
                    if (cAxis == CartesianAxis.X) normal[0] = 1;
                    else if (cAxis == CartesianAxis.Y) normal[1] = 1;
                    else normal[2] = 1;
                    break;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)Axis;
                    if (cyAxis == CylindricalAxis.Z) normal[2] = 1;
                    else normal[1] = 1;
                    break;
                default:
                    normal[2] = 1;
                    break;
            }

            return normal;
        }

        private Feature GetBasePlane(ModelDoc2 swModel)
        {
            string planeName;

            switch (CoordSystem)
            {
                case CoordinateSystemType.Cartesian:
                    CartesianAxis cAxis = (CartesianAxis)Axis;
                    planeName = cAxis == CartesianAxis.X ? "Right Plane" :
                                cAxis == CartesianAxis.Y ? "Top Plane" : "Front Plane";
                    break;
                case CoordinateSystemType.Cylindrical:
                    CylindricalAxis cyAxis = (CylindricalAxis)Axis;
                    planeName = cyAxis == CylindricalAxis.Z ? "Front Plane" : "Top Plane";
                    break;
                default:
                    planeName = "Front Plane";
                    break;
            }

            return FindFeatureByName(swModel, planeName);
        }

        private Feature FindFeatureByName(ModelDoc2 swModel, string name)
        {
            Feature feat = (Feature)swModel.FirstFeature();
            while (feat != null)
            {
                if (feat.Name == name)
                    return feat;
                feat = (Feature)feat.GetNextFeature();
            }
            return null;
        }

        private void CreateCoordinateSystem(ModelDoc2 swModel, FeatureManager featMgr,
            double[] position, int index, CAD_SketchPlane sketchPlane)
        {
            swModel.ClearSelection2(true);

            string planeName = sketchPlane.Path + sketchPlane.Name;
            ModelDocExtension swExt = swModel.Extension;

            swExt.SelectByID2(planeName, "PLANE", position[0], position[1], position[2],
                false, 1, null, 0);

            Feature csysFeat = (Feature)featMgr.InsertCoordinateSystem(false, false, false);

            string csysName = "Station_CSYS_" + (index + 1);
            if (csysFeat != null)
            {
                csysFeat.Name = csysName;
            }

            CoordinateSystem csys = new CoordinateSystem();
            csys.OriginLocation = new Mathematics.Point
            {
                X_Value = position[0],
                Y_Value = position[1],
                Z_Value_Cartesian = position[2]
            };
            sketchPlane.MyCoordinateSystem = csys;
        }

        private void CreateSketchOnPlane(ModelDoc2 swModel, CAD_SketchPlane sketchPlane, int index)
        {
            Feature planeFeat = FindFeatureByName(swModel, sketchPlane.Path + sketchPlane.Name);
            if (planeFeat != null)
            {
                planeFeat.Select2(false, 0);
            }

            swModel.InsertSketch2(true);
            swModel.InsertSketch2(false);

            string sketchName = "Station_Sketch_" + (index + 1);
            Feature sketchFeat = FindFeatureByName(swModel, "Sketch" + (index + 1));
            if (sketchFeat != null)
            {
                sketchFeat.Name = sketchName;
            }

            CAD_Sketch sketch = new CAD_Sketch(sketchName);
            sketchPlane.AddSketch(sketch);
        }
    }
}
