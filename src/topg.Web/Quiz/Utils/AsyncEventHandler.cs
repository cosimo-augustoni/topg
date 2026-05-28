namespace topg.Web.Quiz;

public delegate Task AsyncEventHandler<in T>(object sender, T e) where T : EventArgs;