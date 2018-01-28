﻿using System;
using Autofac;
using Lykke.Cqrs;

namespace Lykke.Job.TransactionHandler.Modules
{
    internal class AutofacDependencyResolver : IDependencyResolver
    {
        private readonly IComponentContext _context;

        public AutofacDependencyResolver(IComponentContext kernel)
        {
            _context = kernel ?? throw new ArgumentNullException("kernel");
        }

        public object GetService(Type type)
        {
            return _context.Resolve(type);
        }

        public bool HasService(Type type)
        {
            return _context.IsRegistered(type);
        }
    }
}