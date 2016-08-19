1. Set the source as the directory that contains the information you want to clone
2. Set the destination as the directory where you want the files to be synced to (create a shared network folder on your raspberry pi, and mount it as a network drive on your PC)
3. Click the start button. The program will begin to copy items from the source to the destination
4. Any changes made to the source are also automatically done to the destination. Ensure that you have privileges to read and write to the samba shared folder
	- run "sudo chmod -R 777 <directory-path>" on your raspberry pi, if you're on a safe private network