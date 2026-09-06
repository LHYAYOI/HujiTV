const SUPABASE_URL = "https://mlzzduyeeptfmmaflgkj.supabase.co";
const SUPABASE_KEY = "sb_publishable_Hev5d_eUSXb0Kjd-wX-Stg_aYnB7Oq9";

async function loadRanking() {
    const status = document.getElementById("status");
    const table = document.getElementById("ranking-table");
    const rankingBody = document.getElementById("ranking-body");

    try {
        const response = await fetch(
            `${SUPABASE_URL}/rest/v1/scores?select=player_name,score&order=score.desc&limit=10`,
            {
                headers:
                {
                    "apikey": SUPABASE_KEY
                }
            }
        );

        if (!response.ok) {
            throw new Error(
                `ランキング取得失敗: ${response.status} ${response.statusText}`
            );
        }

        const scores = await response.json();

        rankingBody.innerHTML = "";

        scores.forEach((scoreData, index) => {
            const row = document.createElement("tr");

            const rankCell = document.createElement("td");
            rankCell.textContent = index + 1;

            const nameCell = document.createElement("td");
            nameCell.textContent = scoreData.player_name;

            const scoreCell = document.createElement("td");
            scoreCell.textContent = scoreData.score;

            row.appendChild(rankCell);
            row.appendChild(nameCell);
            row.appendChild(scoreCell);

            rankingBody.appendChild(row);
        });

        status.hidden = true;
        table.hidden = false;
    }
    catch (error) {
        console.error(error);

        status.textContent =
            "ランキングの取得に失敗しました。";
    }
}

loadRanking();