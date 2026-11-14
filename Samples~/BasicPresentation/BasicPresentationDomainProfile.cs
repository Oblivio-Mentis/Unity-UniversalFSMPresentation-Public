#nullable enable
using System.Collections.Generic;
using OblivioMentis.UniversalFSM;
using UnityEngine;

namespace OblivioMentis.Presentation.Samples
{
    public sealed class BasicPresentationContext { }

    [UniversalFsmStateMetadata("PresentationSample", "Покой", "Пример")]
    public sealed class BasicIdleState : BaseUniversalFsmState<BasicPresentationContext>
    {
        public override int StateId => 1;
    }

    [UniversalFsmStateMetadata("PresentationSample", "Бег", "Пример")]
    public sealed class BasicRunState : BaseUniversalFsmState<BasicPresentationContext>
    {
        public override int StateId => 2;
    }

    [UniversalFsmStateMetadata("PresentationSample", "Прыжок", "Пример")]
    public sealed class BasicJumpState : BaseUniversalFsmState<BasicPresentationContext>
    {
        public override int StateId => 3;
    }

    [UniversalFsmStateMetadata("PresentationSample", "Атака", "Пример")]
    public sealed class BasicAttackState : BaseUniversalFsmState<BasicPresentationContext>
    {
        public override int StateId => 4;
    }

    public sealed class BasicPresentationDomainProfile : UniversalFsmDomainProfile<BasicPresentationContext>
    {
        public override string DomainId => "PresentationSample";
        protected override UniversalFsmRuntimeComposition<BasicPresentationContext> CreateComposition(GameObject owner, UniversalFsmDefinition definition) =>
            new(new BasicPresentationContext(), new Dictionary<int, IUniversalFsmState<BasicPresentationContext>>
            {
                [1] = new BasicIdleState(), [2] = new BasicRunState(),
                [3] = new BasicJumpState(), [4] = new BasicAttackState()
            }, new UniversalConditionRegistry<BasicPresentationContext>());
    }
}
