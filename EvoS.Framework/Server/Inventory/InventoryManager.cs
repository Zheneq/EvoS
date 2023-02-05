using System;
using System.Collections.Generic;
using EvoS.Framework.Network.Static;

namespace EvoS.DirectoryServer.Inventory
{
    public class InventoryManager
    {
        public static List<int> GetUnlockedBannerIDs(long accountId)
        {
            return new List<int>() { 107, 106, 105, 104, 103, 102, 101, 100, 99, 98, 96, 95, 93, 92, 91, 90, 89, 88, 87, 86, 85, 83, 82, 81, 80, 79, 78, 77, 76, 75, 74, 73, 70, 68, 67, 64, 63, 62, 61, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 210, 211, 212, 213, 214, 215, 251, 252, 253, 239, 240, 241, 245, 246, 247, 97, 94, 84, 69, 116, 117, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 170, 171, 186, 187, 188, 189, 196, 197, 198, 199, 223, 224, 225, 226, 227, 228, 229, 230, 254, 255, 256, 257, 65, 242, 243, 244, 236, 237, 238, 248, 249, 250, 260, 261, 262, 263, 264, 265, 266, 267, 222, 281, 282, 283, 284, 222, 295, 296, 297, 298, 277, 278, 279, 280, 299, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322, 323, 329, 330, 331, 332, 336, 337, 338, 339, 340, 341, 342, 343, 345, 347, 350, 351, 352, 353, 354, 355, 333, 334, 335, 361, 362, 363, 364, 365, 366, 367, 368, 369, 370, 358, 359, 360, 357, 376, 377, 378, 371, 372, 373, 374, 379, 380, 381, 382, 286, 390, 388, 391, 387, 386, 389, 392, 393, 398, 399, 400, 401, 402, 403, 404, 405, 406, 407, 408, 409, 410, 411, 412, 423, 424, 426, 427, 428, 429, 415, 414, 416, 413, 379, 380, 381, 382, 217, 430, 431, 432, 433, 434, 436, 435, 438, 437, 439, 440, 441, 444, 446, 442, 443, 452, 453, 454, 455, 456, 457, 458, 459, 460, 38, 461, 462, 463, 464, 465, 466, 324, 325, 326, 327, 328, 356, 471, 472, 473, 474, 467, 468, 475, 476, 477, 478, 479, 480, 481, 482 };
        }

        public static List<int> GetUnlockedEmojiIDs(long accountId)
        {
            return new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 25, 21, 26, 27, 31, 30, 28, 29, 32, 33, 34, 35, 38, 37, 36, 39, 22, 23, 24, 41, 42, 43, 44, 45 };
        }

        public static List<int> GetUnlockedLoadingScreenBackgroundIds(long accountId)
        {
            return new List<int>() { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 1 };
        }

        public static Dictionary<int, bool> GetActivatedLoadingScreenBackgroundIds(long accountId)
        {
            Dictionary<int, bool> backgrounds = new Dictionary<int, bool>();

            for (int i = 1; i <= 18; i++)
            {
                backgrounds.Add(i, true);
            }

            return backgrounds;
        }

        public static List<int> GetUnlockedOverconIDs(long accountId)
        {
            return new List<int>() { 4, 1, 2, 9, 10, 5, 3, 7, 8, 6, 12, 13, 11, 19, 20, 5, 16, 17, 15, 18, 14, 21, 24, 25, 26, 27, 28, 29, 29, 39, 37, 38, 41, 40, 43, 42, 44, 45, 46, 47, 30, 31, 32, 33, 34 };
        }

        public static List<int> GetUnlockedTitleIDs(long accountId)
        {
            //TODO
            return new List<int>() { 33, 34, 35, 36, 37, 17, 25, 24, 23, 22, 21, 20, 19, 18, 16, 15, 14, 13, 12, 10, 8, 7, 6, 27, 30, 31, 38, 41, 43, 44, 45, 46, 50, 51, 57, 58, 59, 61, 63, 66, 67, 68, 69, 70, 64, 71, 72, 73, 74, 75, 78, 81, 83, 84, 85, 86, 87, 88, 89, 91, 92, 93, 95, 106, 107, 108, 109, 110, 111, 112, 113, 114, 115, 116, 117, 118, 119, 120, 121, 123, 124, 126, 127, 128, 129, 130, 131, 132, 133, 134, 135, 136, 125, 137, 138, 139, 140, 141, 142, 143, 144, 145, 146, 147, 148, 149 };
        }

        public static List<int> GetUnlockedRibbonIDs(long accountId)
        {
            //TODO
            return new List<int>();
        }

        public static InventoryComponent GetInventoryComponent(long accountId)
        {
            // TODO
            return new InventoryComponent();
        }

        public static Boolean BannerIsForeground(int bannerID) 
        {
            List<int> fg = new List<int>{ 96, 73, 70, 68, 67, 64, 63, 62, 201, 203, 205, 207, 209, 211, 213, 215, 251, 252, 253, 239, 240, 241, 245, 246, 247, 69, 142, 143, 144, 145, 146, 147, 148, 149, 150, 151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166, 167, 168, 169, 187, 189, 197, 199, 224, 226, 227, 229, 256, 257, 65, 261, 263, 265, 267, 222, 282, 284, 222, 296, 298, 278, 280, 299, 300, 301, 302, 303, 304, 305, 306, 307, 308, 309, 310, 311, 312, 313, 314, 315, 316, 317, 318, 319, 320, 321, 322, 323, 331, 332, 333, 334, 335, 361, 362, 363, 364, 365, 366, 367, 369, 376, 377, 378, 373, 374, 379, 381, 390, 388, 391, 387, 386, 389, 423, 426, 427, 415, 416, 379, 381, 217, 430, 431, 434, 436, 435, 438, 437, 439, 440, 444, 446, 442, 452, 453, 456, 458, 459, 460, 461, 462, 465, 466, 324, 325, 326, 327, 328, 356, 471, 473, 467, 475, 476, 477, 478, 479, 480, 481, 482 };
            return fg.Contains(bannerID);
        }

        internal class VfxCost
        {
            public int VfxId { get; set; }
            public int AbilityId { get; set; }
            public int Cost { get; set; }
        }

        public static int GetVfxCost(int vfxId, int AbilityId)
        {
            List<VfxCost> vfxList = new List<VfxCost>
            {
                // BattleMonk
                new VfxCost { VfxId = 200, AbilityId = 0, Cost = 5000 },
                // BazookaGirl
                new VfxCost { VfxId = 300, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 300, AbilityId = 2, Cost = 1500 },
                // DigitalSorceress
                new VfxCost { VfxId = 400, AbilityId = 1, Cost = 5000 },
                new VfxCost { VfxId = 400, AbilityId = 2, Cost = 1200 },
                new VfxCost { VfxId = 400, AbilityId = 3, Cost = 1200 },
                new VfxCost { VfxId = 400, AbilityId = 4, Cost = 1500 },
                new VfxCost { VfxId = 401, AbilityId = 0, Cost = 1200 },
                // Gremlins
                new VfxCost { VfxId = 300, AbilityId = 0, Cost = 5000 },
                // NanoSmith
                new VfxCost { VfxId = 600, AbilityId = 3, Cost = 1500 },
                new VfxCost { VfxId = 600, AbilityId = 0, Cost = 5000 },
                // RageBeast 
                new VfxCost { VfxId = 700, AbilityId = 0, Cost = 5000 },
                // RobotAnimal
                new VfxCost { VfxId = 800, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 800, AbilityId = 4, Cost = 1500 },
                // Scoundrel
                new VfxCost { VfxId = 900, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 900, AbilityId = 1, Cost = 1500 },
                // Sniper
                new VfxCost { VfxId = 1000, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1000, AbilityId = 4, Cost = 1500 },
                // SpaceMarine
                new VfxCost { VfxId = 1100, AbilityId = 0, Cost = 5000 },
                // Spark
                new VfxCost { VfxId = 1200, AbilityId = 0, Cost = 5000 },
                // TeleportingNinja
                new VfxCost { VfxId = 1300, AbilityId = 0, Cost = 5000 },
                // Thief
                new VfxCost { VfxId = 1400, AbilityId = 0, Cost = 5000 },
                // Tracker
                new VfxCost { VfxId = 1500, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1500, AbilityId = 1, Cost = 1200 },
                // Trickster
                new VfxCost { VfxId = 1600, AbilityId = 0, Cost = 1500 },
                new VfxCost { VfxId = 1600, AbilityId = 1, Cost = 1200 },
                // Rampart
                new VfxCost { VfxId = 1800, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 1801, AbilityId = 3, Cost = 1200 },
                // Claymore
                new VfxCost { VfxId = 1900, AbilityId = 0, Cost = 5000 },
                // Blaster
                new VfxCost { VfxId = 2000, AbilityId = 0, Cost = 5000 },
                // FishMan
                new VfxCost { VfxId = 2100, AbilityId = 0, Cost = 5000 },
                // Exo
                new VfxCost { VfxId = 2200, AbilityId = 0, Cost = 5000 },
                // Soldier (id 2301 ability 4 unable to get normaly)
                new VfxCost { VfxId = 2300, AbilityId = 0, Cost = 5000 },
                // Martyr
                new VfxCost { VfxId = 2400, AbilityId = 0, Cost = 5000 },
                // Sensei
                new VfxCost { VfxId = 2500, AbilityId = 0, Cost = 5000 },
                // Manta
                new VfxCost { VfxId = 2700, AbilityId = 0, Cost = 5000 },
                // Valkyrie
                new VfxCost { VfxId = 2800, AbilityId = 0, Cost = 5000 },
                // Archer
                new VfxCost { VfxId = 2900, AbilityId = 0, Cost = 5000 },
                new VfxCost { VfxId = 2900, AbilityId = 0, Cost = 1200 },
                // Samurai
                new VfxCost { VfxId = 3200, AbilityId = 0, Cost = 5000 },
                // Cleric
                new VfxCost { VfxId = 3400, AbilityId = 0, Cost = 5000 },
                // Neko
                new VfxCost { VfxId = 3500, AbilityId = 0, Cost = 5000 },
                // Scamp
                new VfxCost { VfxId = 3600, AbilityId = 0, Cost = 5000 },
                // Dino
                new VfxCost { VfxId = 3500, AbilityId = 0, Cost = 5000 },
                // Iceborg
                new VfxCost { VfxId = 3900, AbilityId = 0, Cost = 5000 },
                // Fireborg
                new VfxCost { VfxId = 4000, AbilityId = 0, Cost = 5000 }
            };
            VfxCost result = vfxList.Find(m => (m.AbilityId == AbilityId) && (m.VfxId == vfxId));
            return result.Cost;
        }
    }
}
