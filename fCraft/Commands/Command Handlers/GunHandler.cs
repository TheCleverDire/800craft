﻿using System;
using fCraft.Collections;
using System.Linq;
using System.Text;
using fCraft.Events;
using System.Threading;

namespace fCraft
{
    class GunClass
    {
        public static void Init()
        {
            Player.Clicking += ClickedGlass;//
            Player.Moving += gunMove;//
            Player.JoinedWorld += changedWorld;//
            Player.Moving += movePortal;//
            Player.Disconnected += playerDisconnected;//
            Player.PlacingBlock += playerPlaced;
            CommandManager.RegisterCommand(CdGun);
        }
        static readonly CommandDescriptor CdGun = new CommandDescriptor
        {
            Name = "Gun",
            Category = CommandCategory.Moderation,
            IsConsoleSafe = false,
            Permissions = new[] { Permission.Gun },
            Usage = "/Gun",
            Help = "Fire At Will! TNT blocks explode TNT with physics on, Blue blocks make a Blue Portal, Orange blocks make an Orange Portal.",
            Handler = GunHandler
        };

        static void GunHandler(Player player, Command cmd)
        {
            if (player.GunMode)
            {
                player.GunMode = false;
                try
                {
                    foreach (Vector3I block in player.GunCache.Values)
                    {
                        player.Send(PacketWriter.MakeSetBlock(block.X, block.Y, block.Z, player.WorldMap.GetBlock(block)));
                        Vector3I removed;
                        player.GunCache.TryRemove(block.ToString(), out removed);
                    }
                    if (player.bluePortal.Count > 0)
                    {
                        int i = 0;
                        foreach (Vector3I block in player.bluePortal)
                        {
                            if (player.WorldMap != null && player.World.IsLoaded)
                            {
                                player.WorldMap.QueueUpdate(new BlockUpdate(null, block, player.blueOld[i]));
                                i++;
                            }
                        }
                        player.blueOld.Clear();
                        player.bluePortal.Clear();
                    }
                    if (player.orangePortal.Count > 0)
                    {
                        int i = 0;
                        foreach (Vector3I block in player.orangePortal)
                        {
                            if (player.WorldMap != null && player.World.IsLoaded)
                            {
                                player.WorldMap.QueueUpdate(new BlockUpdate(null, block, player.orangeOld[i]));
                                i++;
                            }
                        }
                        player.orangeOld.Clear();
                        player.orangePortal.Clear();
                    }
                    player.Message("&SGunMode deactivated");
                }
                catch (Exception ex)
                {
                    Logger.Log(LogType.SeriousError, "" + ex);
                }
            }
            else
            {
                if (!player.World.gunPhysics)
                {
                    player.Message("&WGun physics are disabled on this world");
                    return;
                }
                player.GunMode = true;
                player.Message("&SGunMode activated. Fire at will!");
            }
        }

        public static void playerPlaced(object sender, PlayerPlacingBlockEventArgs e)
        {
            try
            {
                foreach (Player p in e.Player.World.Players)
                {
                    if (e.OldBlock == Block.Water || e.OldBlock == Block.Lava)
                    {
                        if (p.orangePortal.Contains(e.Coords) || p.bluePortal.Contains(e.Coords))
                        {
                            e.Result = CanPlaceResult.Revert;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

		private static TntBulletBehavior _tntBulletBehavior=new TntBulletBehavior();

        public static void ClickedGlass(object sender, PlayerClickingEventArgs e)
        {
            if (e.Player.GunMode)
            {
                World world = e.Player.World;
                Map map = e.Player.World.Map;
                if (e.Player.GunCache.Values.Contains(e.Coords))
                {
                    e.Player.Send(PacketWriter.MakeSetBlock(e.Coords.X, e.Coords.Y, e.Coords.Z, Block.Glass));
					if (e.Player.LastUsedBlockType == Block.TNT && world.tntPhysics)
					{
						if (e.Player.CanFireTNT())
						{
							double ksi = 2.0*Math.PI*(-e.Player.Position.L)/256.0;
							double r = Math.Cos(ksi);
							double phi = 2.0*Math.PI*(e.Player.Position.R - 64)/256.0;
							Vector3F dir = new Vector3F((float) (r*Math.Cos(phi)), (float) (r*Math.Sin(phi)), (float) (Math.Sin(ksi)));
                            Vector3I start = new Vector3I((short)(e.Coords.X +(r * Math.Cos(phi)) * 3), (short)(e.Coords.Y +(r * Math.Sin(phi)) * 3), (short)(e.Coords.Z+ (Math.Sin(ksi) * 3)));
							world.AddTask(new Particle(world, start, dir, e.Player, Block.TNT, _tntBulletBehavior), 0);
						}
					}
					else
						world.AddTask(new Bullet(world, e.Coords, e.Player.Position, e.Player), 0);
                }
            }
        }


        public static void movePortal(object sender, PlayerMovingEventArgs e)
        {
            try
            {
                if (e.Player.LastUsedPortal != null && (DateTime.Now - e.Player.LastUsedPortal).TotalSeconds < 4)
                {
                    return;
                }
                Vector3I newPos = new Vector3I(e.NewPosition.X / 32, e.NewPosition.Y / 32, (e.NewPosition.Z / 32));
                foreach (Player p in e.Player.World.Players)
                {
                    foreach (Vector3I block in p.bluePortal)
                    {
                        if (newPos == block)
                        {
                            if (p.World.Map.GetBlock(block) == Block.Water)
                            {
                                if (p.orangePortal.Count > 0)
                                {
                                    e.Player.TeleportTo(new Position
                                    {
                                        X = (short)(((p.orangePortal[0].X) + 0.5) * 32),
                                        Y = (short)(((p.orangePortal[0].Y) + 0.5) * 32),
                                        Z = (short)(((p.orangePortal[0].Z) + 1.59375) * 32),
                                        R = (byte)(p.blueOut - 128),
                                        L = e.Player.Position.L
                                    });
                                }
                                e.Player.LastUsedPortal = DateTime.Now;
                            }
                        }
                    }

                    foreach (Vector3I block in p.orangePortal)
                    {
                        if (newPos == block)
                        {
                            if (p.World.Map.GetBlock(block) == Block.Lava)
                            {
                                if (p.bluePortal.Count > 0)
                                {
                                    e.Player.TeleportTo(new Position
                                    {
                                        X = (short)(((p.bluePortal[0].X + 0.5)) * 32),
                                        Y = (short)(((p.bluePortal[0].Y + 0.5)) * 32),
                                        Z = (short)(((p.bluePortal[0].Z) + 1.59375) * 32), //fixed point 1.59375 lol.
                                        R = (byte)(p.orangeOut - 128),
                                        L = e.Player.Position.L
                                    });
                                }
                                e.Player.LastUsedPortal = DateTime.Now;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void changedWorld(object sender, PlayerJoinedWorldEventArgs e)
        {
            try
            {
                if (e.OldWorld != null)
                {
                    if (e.OldWorld.Name == e.NewWorld.Name)
                    {
                        e.Player.orangeOld.Clear();
                        e.Player.orangePortal.Clear();
                        e.Player.blueOld.Clear();
                        e.Player.bluePortal.Clear();
                    }
                    if (e.OldWorld.IsLoaded)
                    {
                        Map map = e.OldWorld.Map;
                        if (e.Player.orangePortal.Count > 0)
                        {
                            int i = 0;
                            foreach (Vector3I block in e.Player.orangePortal)
                            {
                                map.QueueUpdate(new BlockUpdate(null, block, e.Player.orangeOld[i]));
                                i++;
                            }
                            e.Player.orangeOld.Clear();
                            e.Player.orangePortal.Clear();
                        }

                        if (e.Player.bluePortal.Count > 0)
                        {
                            int i = 0;
                            foreach (Vector3I block in e.Player.bluePortal)
                            {
                                map.QueueUpdate(new BlockUpdate(null, block, e.Player.blueOld[i]));
                                i++;
                            }
                            e.Player.blueOld.Clear();
                            e.Player.bluePortal.Clear();
                        }
                    }
                    else
                    {
                        if (e.Player.bluePortal.Count > 0)
                        {
                            e.OldWorld.Map.Blocks[e.OldWorld.Map.Index(e.Player.bluePortal[0])] = (byte)e.Player.blueOld[0];
                            e.OldWorld.Map.Blocks[e.OldWorld.Map.Index(e.Player.bluePortal[1])] = (byte)e.Player.blueOld[1];
                            e.Player.blueOld.Clear();
                            e.Player.bluePortal.Clear();
                        }
                        if (e.Player.orangePortal.Count > 0)
                        {
                            e.OldWorld.Map.Blocks[e.OldWorld.Map.Index(e.Player.orangePortal[0])] = (byte)e.Player.orangeOld[0];
                            e.OldWorld.Map.Blocks[e.OldWorld.Map.Index(e.Player.orangePortal[1])] = (byte)e.Player.orangeOld[1];
                            e.Player.orangeOld.Clear();
                            e.Player.orangePortal.Clear();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }

        public static void playerDisconnected(object sender, PlayerDisconnectedEventArgs e)
        {
            try
            {
                if (e.Player.World != null)
                {
                    if (e.Player.World.IsLoaded)
                    {
                        Map map = e.Player.World.Map;
                        if (e.Player.orangePortal.Count > 0)
                        {
                            int i = 0;
                            foreach (Vector3I block in e.Player.orangePortal)
                            {
                                map.QueueUpdate(new BlockUpdate(null, block, e.Player.orangeOld[i]));
                                i++;
                            }
                            e.Player.orangeOld.Clear();
                            e.Player.orangePortal.Clear();
                        }

                        if (e.Player.bluePortal.Count > 0)
                        {
                            int i = 0;
                            foreach (Vector3I block in e.Player.bluePortal)
                            {
                                map.QueueUpdate(new BlockUpdate(null, block, e.Player.blueOld[i]));
                                i++;
                            }
                            e.Player.blueOld.Clear();
                            e.Player.bluePortal.Clear();
                        }
                    }
                    else
                    {
                        if (e.Player.bluePortal.Count > 0)
                        {
                            e.Player.World.Map.Blocks[e.Player.World.Map.Index(e.Player.bluePortal[0])] = (byte)e.Player.blueOld[0];
                            e.Player.World.Map.Blocks[e.Player.World.Map.Index(e.Player.bluePortal[1])] = (byte)e.Player.blueOld[1];
                        }
                        if (e.Player.orangePortal.Count > 0)
                        {
                            e.Player.WorldMap.Blocks[e.Player.WorldMap.Index(e.Player.orangePortal[0])] = (byte)e.Player.orangeOld[0];
                            e.Player.WorldMap.Blocks[e.Player.WorldMap.Index(e.Player.orangePortal[1])] = (byte)e.Player.orangeOld[1];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }
        public static void removal(ConcurrentDictionary<String, Vector3I> bullets, Map map)
        {
            foreach (Vector3I bp in bullets.Values)
            {
                map.QueueUpdate(new BlockUpdate(null,
                    (short)bp.X,
                    (short)bp.Y,
                    (short)bp.Z,
                    Block.Air));
                Vector3I removed;
                bullets.TryRemove(bp.ToString(), out removed);
            }
        }

        public static void gunMove(object sender, PlayerMovingEventArgs e)
        {
            try
            {
                if (e.Player.GunMode)
                {
                    Position p = e.Player.Position;
                    Position pos = new Position();
                    Map map = e.Player.World.Map;
                    double rSin = Math.Sin(((double)(128 - p.R) / 255) * 2 * Math.PI);
                    double rCos = Math.Cos(((double)(128 - p.R) / 255) * 2 * Math.PI);
                    double lCos = Math.Cos(((double)(p.L + 64) / 255) * 2 * Math.PI);

                    short x = (short)(p.X / 32);
                    x = (short)Math.Round(x + (double)(rSin * 3));

                    short y = (short)(p.Y / 32);
                    y = (short)Math.Round(y + (double)(rCos * 3));

                    short z = (short)(p.Z / 32);
                    z = (short)Math.Round(z + (double)(lCos * 3));

                    for (short x2 = (short)(x + 1); x2 >= x - 1; x2--)
                    {
                        for (short y2 = (short)(y + 1); y2 >= y - 1; y2--)
                        {
                            for (short z2 = z; z2 <= z + 1; z2++)
                            {
                                if (map.GetBlock(x2, y2, z2) == Block.Air)
                                {
                                    pos = new Position(x2, y2, z2);
                                    if (!e.Player.GunCache.Values.Contains(new Vector3I(pos.X, pos.Y, pos.Z)))
                                    {
                                        e.Player.Send(PacketWriter.MakeSetBlock(pos.X, pos.Y, pos.Z, Block.Glass));
                                        e.Player.GunCache.TryAdd(pos.ToVector3I().ToString(), pos.ToVector3I());
                                    }
                                }
                            }
                        }
                    }

                    if (CanRemoveBlock(e.Player, e.OldPosition, e.NewPosition))
                    {
                        foreach (Vector3I block in e.Player.GunCache.Values)
                        {
                            e.Player.Send(PacketWriter.MakeSetBlock(block.X, block.Y, block.Z, map.GetBlock(block)));
                            Vector3I removed;
                            e.Player.GunCache.TryRemove(block.ToString(), out removed);
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogType.SeriousError, "" + ex);
            }
        }
        public static bool CanRemoveBlock(Player player, Position oldpos, Position newPos)
        {
            int x = oldpos.X - newPos.X;
            int y = oldpos.Y - newPos.Y;
            int z = oldpos.Z - newPos.Z;
            int r = oldpos.R - newPos.R;
            int l = oldpos.L - newPos.L;

            if (!(x >= -2 && x <= 2) || !(y >= -2 && y <= 2) || !(z >= -3 && z <= 3))
            {
                return true;
            }
            if (!(x >= -2 && x <= 2) || !(y >= -2 && y <= 2) || !(z >= -2 && z <= 2))
            {
                return true;
            }

            if (!(r >= -5 && r <= 5) || !(l >= -5 && l <= 5))
            {
                return true;
            }

            return false;
        }
        public static bool CanPlacePortal(short x, short y, short z, Map map)
        {
            int Count = 0;
            for (short Z = z; Z < z + 2; Z++)
            {
                Block check = map.GetBlock(x, y, Z);
                if (check != Block.Air && check != Block.Water && check != Block.Lava)
                {
                    Count++;
                }
            }
            if (Count == 2)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}