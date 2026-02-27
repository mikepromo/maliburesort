using shared;

public interface IGameClient
{
	Task ReceiveSpin(SpinResultDto result);
	Task ReceiveChat(ChatMessageDto message);
	Task PlayerJoined(PlayerDto player);
	Task PlayerLeft(PlayerDto player);
	Task BetPlaced(BetDto bet);
	Task BalanceUpdate(TxValue val);
}