This is the core module.   has the most basic and core level stuff, every module should depend on this.
the serializeable via Datacontract WCF, acts as property container in the Kontrol ,  are placed in an undoable container.

LEGACY.   UNDO SHOULD BE REDONe wiht different pattern the database doent need to keep this its memory intenisve, its slow , its not optimal.
howver moving forward

to do this .. a data contract script would need to load and resave all the level files and other assets.. spr spirits, etc.

then resave the assets and replace the properties with a new one.     this is just because oits not optimimal. its not ncessary or critical path.

undoable container  is not a great pattern since it mixes special tool code and complexity
there is a mysterious issue in ObservableCollections that either dont broadcast messages or don't persist, it should all replaced with the Bodytemplate like class of thbe velco physics engine and
a more standard MVVM like undo /redo c# code

The logger

only very general stuff goes here that every module would or should want to include as a reference, or say
90% of them.

there is also a timer for performance on pieces or  major sections.
graphics, plugins, wind, physics


note about trace

this is a portable module. yes.. but if TRACE is not defined and one does a rebuild all

TRACE code wil be stubbed.. this is if there is release benchmarking coding using the 
using ( new Trace.ExecTime(fdsf))   




and there is no easy wasy to Conditionally stub it out on the caller side.

building with no TRACE defined would be rare. even on production builds we may 
do a trave level thing for errors and for use in scripting output, with the editor tool.


and that means not using the using{} logger notation..


thats using the state   Core.Trace.  thing  with a logger attached
that sends errors to the server or to a local storage.. or to a display for tech support.


  NEXT

Milestone:
coommit and get code workign tool , exes apps and load levle , last n no ui no dependenices..
update my tools pass.
udpate my todos..


readme .. formulas .. rich colors and text??


see monogame 
nextge 
 TODO VS 2022.


  VS LOW CODE , NO CODE.. WHATS NEW.. 2020  RC WIHT MYRA AND MONOGAME


  https://community.monogame.net/t/when-will-monogame-be-available-for-visual-studio-2022/15953/5


>>>>>>>>> then: myra

