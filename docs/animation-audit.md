# Stage 6: animation ownership and validation

The supplied GLB is still rendered by ImportedChao. Existing procedural poses remain authoritative. The presentation partial resolves the final head, eye surfaces and compatible upright limb transitions; it is not a second behaviour controller.

## Findings and changes

- Eye visibility used to switch immediately in several setters. Eye geometry and its local anchors are retained; the existing eyelid shader now supports continuous opening and opacity, and existing closed/happy/squint curves crossfade.
- Blink timing was absent. Each model has a private irregular timer. Sleeping eyes and distressed swimming suppress ordinary blinking.
- Discovery, idle scanning and action poses wrote the head directly. Discovery, gifts, instruments and social attention now submit bounded attention targets. Thought, lying, crawling and swimming preserve their specific neck choreography.
- Standing-to-thinking and stopping arms could snap. Upright limb rotations/positions ease between compatible targets. Exact grips bypass this blending so hands remain attached to instruments and handshakes.
- Both feet used absolute sine lift, lifting the stance foot too. Lift is restricted to the swing phase. Existing distance-based gait input gets a small individual pace/phase variation; ground sole correction remains the last operation.
- Stance feet sample gentle slopes independently, with a bounded ankle correction. Swing feet remain free; sleep, water, crawling and precise action choreography bypass the correction.
- Soft-body inertia remains in the existing physics pass. Expressive body offsets are bounded and do not accumulate during paused frames.
- Angry crossed eyes shared a boolean with distressed swimming. Expression arbitration now also uses pose context so these actions retain different emotional meanings.
- A long-running head interpolation could accumulate quaternion error. Interpolation endpoints/results are normalized.

## Protected choreography

Swimming/crawling neck compensation, flights, sleep turns, rising with hand support, food bites, precise tool grips, courtship and throw recovery keep their established pose functions. There is no universal whole-body blend across incompatible poses. Facial state, attention timers, blink schedules and transition caches are transient and are not serialized.

## Verification

Pure checks cover memory limits, decay, anti-farming, directed friendship, legacy saves, expression priority and variable blink timing. The Godot personality check covers ten expressions, unchanged eye anchors, neck limits, attention release, independent blink schedules, sleep/swimming priority, thinking transitions and exact grips. Existing tests that assumed instant eye visibility now advance the visual transition before checking it.

The visual rig review captures ten faces and ordinary-camera motion/rest clips; the ordinary discovery review observes real weighted AI rather than a scripted discovery. These are complementary checks, not proof of every possible pose combination. The supplied mesh has no independently rigged eyeballs or facial morph targets; eye opening and original curve geometry provide restrained expressions.
