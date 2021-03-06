﻿#region using directives

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using PoGo.PokeMobBot.Logic.Event;
using PoGo.PokeMobBot.Logic.Tasks;
using PoGo.PokeMobBot.Logic.Utils;
using POGOProtos.Data.Player;
using POGOProtos.Enums;
using POGOProtos.Networking.Responses;

#endregion

namespace PoGo.PokeMobBot.Logic.State
{
    public class CheckTosState : IState
    {
        public async Task<IState> Execute(ISession session, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (session.LogicSettings.AutoCompleteTutorial)
            {
                var tutState = session.Profile.PlayerData.TutorialState;
                if (!tutState.Contains(TutorialState.LegalScreen))
                {
                    await
                        session.Client.Misc.MarkTutorialComplete(new RepeatedField<TutorialState>()
                        {
                            TutorialState.LegalScreen
                        });
                    await DelayingUtils.Delay(7000, 2000);
                }
                if (!tutState.Contains(TutorialState.AvatarSelection))
                {
                    var gen = session.Client.rnd.Next(1) == 0 ? Gender.Male : Gender.Female;
                    var avatarRes = await session.Client.Player.SetAvatar(new PlayerAvatar()
                    {
                        Backpack = 0,
                        Eyes = 0,
                        Gender = gen,
                        Hair = 0,
                        Hat = 0,
                        Pants = 0,
                        Shirt = 0,
                        Shoes = 0,
                        Skin = 0
                    });
                    if (avatarRes.Status == SetAvatarResponse.Types.Status.AvatarAlreadySet ||
                        avatarRes.Status == SetAvatarResponse.Types.Status.Success)
                    {
                        await session.Client.Misc.MarkTutorialComplete(new RepeatedField<TutorialState>()
                        {
                            TutorialState.AvatarSelection
                        });
                    }
                }
                if (!tutState.Contains(TutorialState.PokemonCapture))
                {
                    await CatchFirstPokemon(session);
                }
                if (!tutState.Contains(TutorialState.NameSelection))
                {
                    await SelectNicnname(session);
                }
                if (!tutState.Contains(TutorialState.FirstTimeExperienceComplete))
                {
                    await
                        session.Client.Misc.MarkTutorialComplete(new RepeatedField<TutorialState>()
                        {
                            TutorialState.FirstTimeExperienceComplete
                        });
                    await DelayingUtils.Delay(3000, 2000);
                }
            }
            return new FarmState();
        }

        public async Task<bool> CatchFirstPokemon(ISession session)
        {
            var firstPokeList = new List<PokemonId>
            {
                PokemonId.Bulbasaur,
                PokemonId.Charmander,
                PokemonId.Squirtle
            };

            var firstpokeRnd = session.Client.rnd.Next(0, 2);
            var firstPoke = firstPokeList[firstpokeRnd];

            var res = await session.Client.Encounter.EncounterTutorialComplete(firstPoke);
            await DelayingUtils.Delay(7000, 2000);
            return res.Result == EncounterTutorialCompleteResponse.Types.Result.Success;
        }

        public async Task<bool> SelectNicnname(ISession session)
        {
            var res = await session.Client.Misc.ClaimCodename(session.LogicSettings.DesiredNickname);
            if (res.Status == ClaimCodenameResponse.Types.Status.SUCCESS)
            {
                session.EventDispatcher.Send(new NoticeEvent()
                {
                    Message = $"Your name is now: {res.Codename}"
                });
                await session.Client.Misc.MarkTutorialComplete(new RepeatedField<TutorialState>()
                        {
                            TutorialState.NameSelection
                        });
            }
            else if (res.Status == ClaimCodenameResponse.Types.Status.CODENAME_CHANGE_NOT_ALLOWED || res.Status == ClaimCodenameResponse.Types.Status.CURRENT_OWNER)
            {
                await session.Client.Misc.MarkTutorialComplete(new RepeatedField<TutorialState>()
                        {
                            TutorialState.NameSelection
                        });
            }
            else
            {
                session.EventDispatcher.Send(new NoticeEvent()
                {
                    Message = $"Name selection failed! Error: {res.Status}"
                });
            }
            await DelayingUtils.Delay(3000, 2000);
            return res.Status == ClaimCodenameResponse.Types.Status.SUCCESS;
        }
    }
}