using System;
using System.Linq;
using Autofac;
using Autofac.Core;

namespace bnet.IoC.Log4Net
{
    public abstract class LogModule<TLogger> : Module
    {
        protected abstract TLogger CreateLoggerFor(Type type);


        protected override void AttachToComponentRegistration(IComponentRegistry componentRegistry,
                                                              IComponentRegistration registration)
        {
            var type = registration.Activator.LimitType;
            if (this.HasPropertyDependencyOnLogger(type))
            {
                registration.Activated += this.InjectLoggerViaProperty;
            }

            if (this.HasConstructorDependencyOnLogger(type))
            {
                registration.Preparing += this.InjectLoggerViaConstructor;
            }
        }


        private bool HasPropertyDependencyOnLogger(Type type)
        {
            return type.GetProperties().Any(property => property.CanWrite && property.PropertyType == typeof (TLogger));
        }


        private bool HasConstructorDependencyOnLogger(Type type)
        {
            return type.GetConstructors()
                       .SelectMany(ctor =>
                                   ctor.GetParameters()
                                       .Where(
                                           parameter =>
                                           parameter.ParameterType == typeof (TLogger)))
                       .Any();
        }


        private void InjectLoggerViaProperty(object sender, ActivatedEventArgs<object> @event)
        {
            var type = @event.Instance.GetType();
            var propertyInfo = type.GetProperties().First(x => x.CanWrite && x.PropertyType == typeof (TLogger));
            propertyInfo.SetValue(@event.Instance, this.CreateLoggerFor(type), null);
        }


        private void InjectLoggerViaConstructor(object sender, PreparingEventArgs @event)
        {
            var type = @event.Component.Activator.LimitType;
            @event.Parameters = @event.Parameters.Union(new[]
                {
                    new ResolvedParameter((parameter, context) => parameter.ParameterType == typeof (TLogger),
                                          (p, i) => this.CreateLoggerFor(type))
                });
        }
    }
}

