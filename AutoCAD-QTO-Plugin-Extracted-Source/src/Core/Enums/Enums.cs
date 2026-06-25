namespace QTO.Core.Enums
{
    public enum EntityType
    {
        Line,
        Polyline,
        Polyline3d,
        Arc,
        Circle,
        Block,
        DynamicBlock,
        MText,
        Text,
        Hatch,
        Dimension,
        Spline,
        Ellipse,
        Region,
        Solid3d,
        Unknown
    }

    public enum ExtractionScope
    {
        EntireDrawing,
        SelectionSet,
        SelectedLayers,
        CurrentSpace
    }

    public enum GroupByMode
    {
        Layer,
        ObjectType,
        BlockName,
        Layer_Then_Type
    }

    public enum ExportFormat
    {
        Excel,
        CSV,
        JSON,
        XML,
        All
    }

    public enum ElectricalItemType
    {
        LVCable,
        MVCable,
        CableTray,
        Conduit,
        LightingFixture,
        LightingPole,
        JunctionBox,
        Panel,
        Switch,
        Socket,
        GroundingConductor,
        Manhole,
        Handhole,
        Unknown
    }

    public enum ProcessingStatus
    {
        Idle,
        Scanning,
        Extracting,
        Computing,
        Exporting,
        Complete,
        Error,
        Cancelled
    }
}
