using Xunit;

// Every test class shares the one dev database (prime_dev) and many retire shared
// configuration rows (transaction types, numbering schemes, approval chains) inside
// their rolled-back transactions. Run in parallel, those updates deadlock (Postgres
// 40P01), so the classes run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
