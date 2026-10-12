//-----------------------------------------------
// ICastMagicCommand.cs
// 制作日：2026/10/11
// 制作者：安田晴人
// 概要： 魔法をキャストするコマンドのインターフェース
//-----------------------------------------------
public interface ICastMagicCommand
{
    void Execute(MAGIC_CAST_DIRECTION direction);
}
