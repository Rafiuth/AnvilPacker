using AnvilPacker.Level;

namespace AnvilPacker.Encoder.Transforms
{
    //Notes when writting reversible transforms:
    //- Do not depend on block attributes, as that would add unecessary registry dependency.
    public abstract class ReversibleTransform : TransformBase
    {
        public abstract void Reverse(RegionBuffer region);
    }
}
