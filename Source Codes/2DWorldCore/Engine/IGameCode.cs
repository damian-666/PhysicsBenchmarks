
//using Core.Game.Simulation;


using Core.Game.MG.Simulation;


namespace _2DWorldCore
{
    /// <summary>
    /// This Interface lets us make a ui core of everythign common to all the playforms, and without basing on the confusiong timer and game loop of monogame, view and game update, and its sync wiht graphics refresh.
    /// its basically allows for a game loop with .net sytem timer, ( or using a tight untimed loop for max updates per sec, start, onupdateframe,   and exit)
    /// </summary>
    public interface IGameCode
    {
        void Start();
        void Update(object sender, TickEventArgs e);

 /// <summary>
 ///    this one is 
///     called while physics is locked, but from th backk thread that updates physics engine
 /// </summary>
        void PreUpdatePhysicsBk();

        /// <summary>
        /// this is called on the UI thread context
        /// </summary>
        void PreUpdatePhysics();
        void Terminate();
    }

}
