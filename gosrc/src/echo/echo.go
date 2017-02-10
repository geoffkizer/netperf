package main

import "log"
import "net"

func handle_connection(c net.Conn) {
    buf := make([]byte, 4096)
    
	for {
		readBytes, err := c.Read(buf)
		if err != nil {
			log.Printf("connection read error, err=%s", err)
			c.Close()
			return
		}
        
        log.Printf("read %n bytes", readBytes)
            
        writeBytes, err := c.Write(buf[:readBytes])
		if err != nil {
			log.Printf("connection write error, err=%s", err)
			c.Close()
			return
		}
        
        log.Printf("wrote %n bytes", writeBytes)
	}
}

func main() {
	// Listen on TCP port 5000 on all interfaces.
	listener, err := net.Listen("tcp", ":5000")
	if err != nil {
		log.Fatal(err)
	}
	defer listener.Close()
    
    log.Printf("Server running")
    
    for {
		// Wait for a connection.
		conn, err := listener.Accept()
		if err != nil {
			log.Fatal(err)
		}
        
        log.Printf("Connection accepted")
        
        go handle_connection(conn)
    }
}