using SampleGeneratorGenerated;

namespace NS1
{
    [WrapLogger]
    public partial class Class1
    {
        [WrapLogger]
        public partial class Class2<T>
        {
            [WrapLogger]
            public partial class Class3
            {
            }
        }
    }

    [WrapLogger]
    public partial struct Struct1
    {
    }
}


namespace NS2
{
    namespace NS3
    {
        [WrapLogger]
        public partial class Class1
        {
            [WrapLogger]
            public partial class Class2<T>
            {
                [WrapLogger]
                public partial class Class3
                {
                }
            }
        }
    }
}
