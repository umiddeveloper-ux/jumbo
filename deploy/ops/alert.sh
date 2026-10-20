#!/bin/bash
# Sends a message to the owner in Telegram (the Jumbo bot). Usage: alert.sh "text"
# The bot token is read from the running API container; the chat is in alert-chat-id (the owner gets it by writing /id to the bot).
# Without a chat id the text is only written to the log.
MSG="$1"
DIR=${JUMBO_DEPLOY_DIR:-/opt/littlepolyglot/deploy}
TOKEN=$(docker exec jumbo-api printenv Telegram__BotToken 2>/dev/null)
CHAT=""
[ -f "$DIR/alert-chat-id" ] && CHAT=$(tr -d '[:space:]' < "$DIR/alert-chat-id")
if [ -z "$TOKEN" ] || [ -z "$CHAT" ]; then
  echo "$(date -Is) ALERT (not sent: no token or no chat id): $MSG"
  exit 0
fi
curl -s -m 20 -o /dev/null "https://api.telegram.org/bot$TOKEN/sendMessage" \
  --data-urlencode "chat_id=$CHAT" --data-urlencode "text=$MSG"
echo "$(date -Is) ALERT sent: $MSG"
