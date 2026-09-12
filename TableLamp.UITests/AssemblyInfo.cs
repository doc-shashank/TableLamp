using Xunit;

// Disable test parallelization for UI tests to avoid desktop focus and coordinate collisions
[assembly: CollectionBehavior(DisableTestParallelization = true)]
