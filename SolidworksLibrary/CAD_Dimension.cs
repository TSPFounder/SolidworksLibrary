using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mathematics;
using SE_Library;

namespace CAD
{
    public class CAD_Dimension : CAD_DrawingElement
    {
        //  *****************************************************************************************
        //  DECLARATIONS
        //
        //  ************************************************************
        #region
        //
        //  Identification
        private String _DimensionID;
        private String _Description;
        private Boolean _IsOrdinate;
        //
        //  Dimension Data        
        private Double _DimensionNominalValue;
        private Double _DimensionUpperLimitValue;
        private Double _DimensionLowerLimitValue;
        private DimensionType _MyDimensionType;
        private UnitOfMeasure _EngineeringUnit;
        //
        //  Center Point
        private Point _CenterPoint;
        //
        //  Leader Line End Point
        private Point _LeaderLineEndPoint;
        //
        //  Leader Line Bend Point
        private Point _LeaderLineBendPoint;
        //
        //  Owned & Owning Objects
        //
        //  CAD Models
        private CAD_Model _MyModel;
        //
        //  Points
        private Point _DimensionPoint;
        private Point _ReferencePoint;
        //
        //  Segments
        private Segment _MySegment;
        //
        //  Parameters
        private CAD_Parameter _CurrentParameter;
        private List<CAD_Parameter> _MyParameters;

        #endregion
        //  *****************************************************************************************


        //  ****************************************************************************************
        //  INITIALIZATIONS
        //
        //  ************************************************************

        //  *****************************************************************************************


        //  *****************************************************************************************
        //  ENUMERATIONS
        //
        //  ************************************************************
        public enum DimensionType
        {
            Length = 0,
            Diameter,
            Radius,
            Angle,
            Distance,
            Ordinal,
            Other
        }
        //  *****************************************************************************************


        //  *****************************************************************************************
        //  CAD_DIMENSION CONSTRUCTOR
        //
        //  ************************************************************
        public CAD_Dimension()
        {
            this.MyType = DrawingElementType.Dimension;
            //
            //  Locating Objects
            //
            //  Center Point
            this.CenterPoint = new Point();
            //
            //  Leader Line End Point
        }
        //  *****************************************************************************************


        //  *****************************************************************************************
        //  PROPERTIES
        //
        //  ************************************************************
        //
        //  Identification
        //
        //  Dimension ID
        public String DimensionID
        {
            set => _DimensionID = value;
            get
            {
                return _DimensionID;
            }
        }
        //       
        //  Description
        public String Description
        {
            set => _Description = value;
            get
            {
                return _Description;
            }
        }
        //
        //  Is an Ordinate Dimension
        public Boolean IsOrdinate
        {
            set => _IsOrdinate = value;
            get
            {
                return _IsOrdinate;
            }
        }
        //
        //  Center Point
        public Point CenterPoint
        {
            set => _CenterPoint = value;
            get { return _CenterPoint; }
        }
        //
        //  Leader Line End Point
        public Point LeaderLineEndPoint
        {
            set => _LeaderLineEndPoint = value;
            get { return _LeaderLineEndPoint; }
        }
        //
        //  Leader Line Bend Point
        public Point LeaderLineBendPoint
        {
            set => _LeaderLineBendPoint = value;
            get { return _LeaderLineBendPoint; }
        }
        //
        //  Owned & Owning Objects
        //
        //  CAD Model
        public CAD_Model MyModel
        {
            set => _MyModel = value;
            get
            {
                return _MyModel;
            }
        }
        //
        //  Dimension Data
        //
        //  Dimension Point
        public Point DimensionPoint
        {
            set => _DimensionPoint = value;
            get
            {
                return _DimensionPoint;
            }
        }
        //
        //  Reference Point
        public Point ReferencePoint
        {
            set => _ReferencePoint = value;
            get
            {
                return _ReferencePoint;
            }
        }
        //
        //  My Segment
        public Segment MySegment
        {
            set => _MySegment = value;
            get
            {
                return _MySegment;
            }
        }
        //
        //  Dimension Values
        //
        //  Nominal Value
        public Double DimensionNominalValue
        {
            set => _DimensionNominalValue = value;
            get
            {
                return _DimensionNominalValue;
            }
        }
        //
        //  Upper Limit Value
        public Double DimensionUpperLimitValue
        {
            set => _DimensionUpperLimitValue = value;
            get
            {
                return _DimensionUpperLimitValue;
            }
        }
        //
        //  Lower Limit Value
        public Double DimensionLowerLimitValue
        {
            set => _DimensionLowerLimitValue = value;
            get
            {
                return _DimensionLowerLimitValue;
            }
        }
        //
        //  Dimension Type
        public DimensionType MyDimensionType
        {
            set => _MyDimensionType = value;
            get
            {
                return _MyDimensionType;
            }
        }
        //
        //  Engineering Unit
        public UnitOfMeasure EngineeringUnit
        {
            set => _EngineeringUnit = value;
            get
            {
                return _EngineeringUnit;
            }
        }
        //
        //  Parameters
        //
        //  Current Parameter
        public CAD_Parameter CurrentParameter
        {
            set => _CurrentParameter = value;
            get
            {
                return _CurrentParameter;
            }
        }
        //
        //  My Parameters
        public List<CAD_Parameter> MyParameters
        {
            set => _MyParameters = value;
            get
            {
                return _MyParameters;
            }
        }
        //  *****************************************************************************************


        //  *****************************************************************************************
        //  METHODS
        //
        //  ************************************************************

        //  *****************************************************************************************


        //  *****************************************************************************************
        //  EVENTS
        //
        //  ************************************************************

        //  *****************************************************************************************
    }
}
