#nullable enable
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation.PlayMode.Tests
{
    public sealed class RegressionContext { }
    [UniversalFsmStateMetadata("PresentationRegression", "Idle", "Main")]
    public sealed class RegressionIdleState : BaseUniversalFsmState<RegressionContext>
    {
        public override int StateId => 1;
    }
    public sealed class RegressionDomainProfile : UniversalFsmDomainProfile<RegressionContext>
    {
        public override string DomainId => "PresentationRegression";
        protected override UniversalFsmRuntimeComposition<RegressionContext> CreateComposition(GameObject owner, UniversalFsmDefinition definition) =>
            new(new RegressionContext(), new Dictionary<int, IUniversalFsmState<RegressionContext>> { [1] = new RegressionIdleState() }, new UniversalConditionRegistry<RegressionContext>());
    }
}
