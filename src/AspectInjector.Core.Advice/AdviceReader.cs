﻿using AspectInjector.Broker;
using AspectInjector.Core.Advice.Effects;
using AspectInjector.Core.Contracts;
using AspectInjector.Core.Extensions;
using AspectInjector.Core.Models;
using AspectInjector.Rules;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AspectInjector.Core.Advice
{
    public class AdviceReader : IEffectReader
    {
        private readonly ILogger _log;

        public AdviceReader(ILogger log)
        {
            _log = log;
        }

        public IReadOnlyCollection<Effect> Read(ICustomAttributeProvider host)
        {
            if (host is MethodDefinition source)
                return Extract(source);

            return new List<Effect>();
        }

        private IReadOnlyCollection<AdviceEffectBase> Extract(MethodDefinition method)
        {
            var advices = new List<AdviceEffectBase>();

            foreach (var ca in method.CustomAttributes.ToList())
            {
                if (ca.AttributeType.FullName == WellKnownTypes.Advice)
                {
                    var adviceType = ca.GetConstructorValue<Kind>(0);
                    var advice = CreateEffect(adviceType);
                    if (advice == null)
                    {
                        _log.Log(GeneralRules.UnknownCompilationOption, method, $"Unknown advice kind '{adviceType.ToString()}'");
                        continue;
                    }

                    advice.Method = method;
                    advice.Target = ca.GetPropertyValue<Target>(nameof(Broker.Advice.Targets));

                    if (advice.Target > Target.Any)
                        _log.Log(GeneralRules.UnknownCompilationOption, method, $"Unknown advice target '{advice.Target.ToString()}'");

                    if ((advice.Target & Target.AnyScope) == 0) advice.Target = advice.Target ^ Target.AnyScope;
                    if ((advice.Target & Target.AnyMember) == 0) advice.Target = advice.Target ^ Target.AnyMember;
                    if ((advice.Target & Target.AnyAccess) == 0) advice.Target = advice.Target ^ Target.AnyAccess;

                    advice.Arguments = ExtractArguments(method);

                    advices.Add(advice);
                }
            }

            return advices;
        }

        private List<AdviceArgument> ExtractArguments(MethodDefinition method)
        {
            var args = new List<AdviceArgument>();

            foreach (var par in method.Parameters)
            {
                var argAttr = par.CustomAttributes.FirstOrDefault(ca => ca.AttributeType.FullName == WellKnownTypes.Argument);
                if (argAttr == null)                
                    _log.Log(EffectRules.AdviceArgumentMustBeBound, method, par.Name);               
                

                var source = argAttr.GetConstructorValue<Source>(0);
                if (!Enum.IsDefined(typeof(Source), source))                
                    _log.Log(GeneralRules.UnknownCompilationOption, method, $"Unknown argument source '{source.ToString()}'");
                

                args.Add(new AdviceArgument
                {
                    Source = argAttr.GetConstructorValue<Source>(0),
                    Parameter = par
                });
            }

            return args;
        }

        internal static AdviceEffectBase CreateEffect(Kind adviceType)
        {
            switch (adviceType)
            {
                case Kind.After: return new AfterAdviceEffect();
                case Kind.Before: return new BeforeAdviceEffect();
                case Kind.Around: return new AroundAdviceEffect();
                default: return null;
            }
        }
    }
}