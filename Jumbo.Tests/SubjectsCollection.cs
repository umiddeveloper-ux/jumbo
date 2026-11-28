namespace Jumbo.Tests;

// The test classes that make topics (subjects) or read "the book a learner is in" share one test database: a topic that one class makes while another one
// works out the current book of a learner (a topic of a lower book that is not won keeps him in that book) made a test fail now and then. A collection runs its
// classes one after another; everything else still runs in parallel.
[CollectionDefinition("subjects")]
public class SubjectsCollection;
