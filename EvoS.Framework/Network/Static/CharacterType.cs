using System;
using EvoS.Framework.Network;

// Token: 0x02000767 RID: 1895
[EvosMessage(47)]
public enum CharacterType
{
    None,
    Archer = 28, // Khita
    BattleMonk = 1, // Asana
    BazookaGirl, // Zuki <3
    Blaster = 19, // Elle
    Claymore = 18, // Titus
    Cleric = 33, // Meridian 
    DigitalSorceress = 3, // Aurora
    Dino = 37, // Magnus
    Exo = 21, // Juno
    Fireborg = 39, // Lex
    FishMan = 20, // Finn
    Gremlins = 4, // Gremolitions
    Gryd = 32, // Unreleased
    Iceborg = 38, // Vonn
    Manta = 26, // Phaedra
    Martyr = 23, // Orion
    NanoSmith = 5, // Helio
    Neko = 34, // Nev
    RageBeast = 6, // Rask
    Rampart = 17, // Rampart
    RobotAnimal = 7, // PuP
    Samurai = 31, // Tol-Ren
    Scamp = 35, // Isadora
    Scoundrel = 8, // Lockwood
    Sensei = 24,// Su-Ren
    Sniper = 9, // Nix
    Soldier = 22, // Blackburn
    SpaceMarine = 10, // Garrison
    Spark, // Quarky <3
    TeleportingNinja, // Kaigin
    Thief, // Celeste
    Tracker, // Grey
    Trickster, // OZ
    Valkyrie = 27, // Brynn

    PunchingDummy = 16,
    PendingWillFill = 25,
    FemaleWillFill = 36,
    TestFreelancer1 = 29,
    TestFreelancer2,
    Last = 40
}
