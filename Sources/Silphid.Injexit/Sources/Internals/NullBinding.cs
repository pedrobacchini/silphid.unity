﻿namespace Silphid.Injexit
{
    internal class NullBinding : IBinding
    {
        public IContainer Container => Injexit.Container.Null;
        public IBinding InList() => Binding.Null;
        public IBinding AsSingle() => Binding.Null;
        public IBinding AsEagerSingle() => Binding.Null;
        public IBinding Using(IResolver resolver) => Binding.Null;
        public IBinding Named(string name) => Binding.Null;
        public IBinding Id(BindingId id) => Binding.Null;
    }
}